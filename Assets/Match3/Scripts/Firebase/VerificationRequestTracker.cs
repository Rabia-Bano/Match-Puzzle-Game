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

        public static void MarkVerified(string uid)
        {
            if (string.IsNullOrEmpty(uid)) return;

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
