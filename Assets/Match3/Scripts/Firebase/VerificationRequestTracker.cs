// ============================================================
//  VerificationRequestTracker.cs  —  static helper  (NEW)
//
//  Problem: kuch players register karke bar-bar "Resend verification
//  email" dabate rehte hain lekin kabhi verify nahi karte (fake /
//  spam email, bot, ya kisi aur ka email). Admin ko iska pata nahi
//  chalta tha, kyun ke unverified player ka players/{uid} document
//  banta hi nahi (profile verification ke BAAD banti hai).
//
//  Solution — alag collection:  verificationRequests/{uid}
//    uid, email, username
//    emailsSent        — total verification emails (pehli + resends)
//    resendCount       — sirf "Resend" button wali requests
//    firstRequestAt, lastRequestAt
//    verified, verifiedAt
//    flagged           — true jab resendCount >= MAX_RESENDS (3)
//    banned, banReason — SIRF admin panel likh sakta hai
//
//  Rules:
//    • Registration wali pehli email count NAHI hoti — sirf resends.
//    • 3 resends ke baad (MAX_RESENDS) → flagged = true → admin panel
//      ki "Verification Alerts" list + notification bell mein aa jata
//      hai, aur 4th resend game mein BLOCK ho jata hai.
//    • Admin wahan se BAN kar sakta hai (reason ke saath). Banned
//      player na resend kar sakta hai, na verify karke andar aa sakta
//      hai, na login kar sakta hai.
//
//  IMPORTANT: MAX_RESENDS yahan aur firestore.rules dono mein SAME
//  hona chahiye (rules mein "resendCount >= 3" wali line).
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using Firebase.Extensions;
using Firebase.Firestore;

namespace Game.Firebase
{
    public static class VerificationRequestTracker
    {
        public const string COLLECTION  = "verificationRequests";
        public const int    MAX_RESENDS = 3;

        public enum ResendDecision { Allowed, LimitReached, Banned, NetworkError }

        private static FirebaseFirestore Db => FirebaseFirestore.DefaultInstance;

        /// <summary>Reads the doc and says whether another resend is allowed.</summary>
        public static void CheckCanResend(string uid, Action<ResendDecision, int> callback)
        {
            if (string.IsNullOrEmpty(uid)) { callback?.Invoke(ResendDecision.NetworkError, 0); return; }

            Db.Collection(COLLECTION).Document(uid).GetSnapshotAsync().ContinueWithOnMainThread(t =>
            {
                if (t.IsFaulted || t.IsCanceled)
                {
                    Debug.LogWarning($"[VerificationTracker] Read failed: {t.Exception?.GetBaseException()?.Message}");
                    callback?.Invoke(ResendDecision.NetworkError, 0);
                    return;
                }

                DocumentSnapshot snap = t.Result;
                if (!snap.Exists) { callback?.Invoke(ResendDecision.Allowed, 0); return; }

                bool banned = snap.ContainsField("banned") && snap.GetValue<bool>("banned");
                int  count  = snap.ContainsField("resendCount") ? snap.GetValue<int>("resendCount") : 0;

                if (banned)                   callback?.Invoke(ResendDecision.Banned, count);
                else if (count >= MAX_RESENDS) callback?.Invoke(ResendDecision.LimitReached, count);
                else                          callback?.Invoke(ResendDecision.Allowed, count);
            });
        }

        /// <summary>
        /// Call AFTER a verification email was actually sent.
        /// isFirstSend = the automatic email at registration (doesn't count as a resend).
        /// previousResendCount = value read by CheckCanResend (0 for first send).
        /// </summary>
        public static void RecordSend(string uid, string email, string username, bool isFirstSend, int previousResendCount)
        {
            if (string.IsNullOrEmpty(uid)) return;

            int newResendCount = isFirstSend ? previousResendCount : previousResendCount + 1;
            string now = DateTime.UtcNow.ToString("o");

            var data = new Dictionary<string, object>
            {
                { "uid",           uid },
                { "email",         email ?? "" },
                { "username",      username ?? "" },
                { "emailsSent",    FieldValue.Increment(1) },
                { "resendCount",   newResendCount },
                { "lastRequestAt", now },
                { "verified",      false },
                { "flagged",       newResendCount >= MAX_RESENDS }
            };
            if (isFirstSend) data["firstRequestAt"] = now;

            Db.Collection(COLLECTION).Document(uid).SetAsync(data, SetOptions.MergeAll).ContinueWithOnMainThread(t =>
            {
                if (t.IsFaulted || t.IsCanceled)
                    Debug.LogWarning($"[VerificationTracker] RecordSend failed: {t.Exception?.GetBaseException()?.Message}");
                else if (newResendCount >= MAX_RESENDS)
                    Debug.LogWarning($"[VerificationTracker] {email} reached {newResendCount} resends without verifying — flagged for admin review.");
            });
        }

        /// <summary>Marks the request as verified (fire-and-forget).</summary>
        public static void MarkVerified(string uid)
        {
            if (string.IsNullOrEmpty(uid)) return;

            // The security rules only accept verified:true when the ID token says
            // email_verified == true. After the player clicks the link, the CACHED
            // token still says false — so force a token refresh first.
            var user = AuthManager.CurrentUser;
            if (user == null) return;

            user.TokenAsync(true).ContinueWithOnMainThread(tokenTask =>
            {
                var data = new Dictionary<string, object>
                {
                    { "uid",        uid },
                    { "verified",   true },
                    { "verifiedAt", DateTime.UtcNow.ToString("o") }
                };
                Db.Collection(COLLECTION).Document(uid).SetAsync(data, SetOptions.MergeAll).ContinueWithOnMainThread(t =>
                {
                    if (t.IsFaulted || t.IsCanceled)
                        Debug.LogWarning($"[VerificationTracker] MarkVerified failed: {t.Exception?.GetBaseException()?.Message}");
                });
            });
        }

        /// <summary>
        /// NEW — used on every normal login: if a verificationRequests doc exists and
        /// still says verified:false, flip it to true. Never creates a doc for players
        /// that never had one.
        /// </summary>
        public static void MarkVerifiedIfPending(string uid)
        {
            if (string.IsNullOrEmpty(uid)) return;
            Db.Collection(COLLECTION).Document(uid).GetSnapshotAsync().ContinueWithOnMainThread(t =>
            {
                if (t.IsFaulted || t.IsCanceled || !t.Result.Exists) return;
                bool verified = t.Result.ContainsField("verified") && t.Result.GetValue<bool>("verified");
                if (!verified) MarkVerified(uid);
            });
        }

        /// <summary>
        /// True if the admin banned this uid from the Verification Alerts page.
        /// On a read error it answers false (never locks a real player out because of
        /// a network hiccup) — the players/{uid}.isBanned check still applies.
        /// </summary>
        public static void CheckBanned(string uid, Action<bool, string> callback)
        {
            if (string.IsNullOrEmpty(uid)) { callback?.Invoke(false, null); return; }

            Db.Collection(COLLECTION).Document(uid).GetSnapshotAsync().ContinueWithOnMainThread(t =>
            {
                if (t.IsFaulted || t.IsCanceled || !t.Result.Exists) { callback?.Invoke(false, null); return; }

                DocumentSnapshot snap = t.Result;
                bool   banned = snap.ContainsField("banned") && snap.GetValue<bool>("banned");
                string reason = snap.ContainsField("banReason") ? snap.GetValue<string>("banReason") : null;
                callback?.Invoke(banned, reason);
            });
        }
    }
}
