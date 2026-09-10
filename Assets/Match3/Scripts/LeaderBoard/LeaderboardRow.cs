// ============================================================
//  LeaderboardRow.cs  —  MonoBehaviour
//  Attach to: the "LeaderboardRow" prefab (one row inside the
//  ScrollRect's Content). See setup guide for the exact child
//  hierarchy this expects.
// ============================================================

using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace Match3
{
    public class LeaderboardRow : MonoBehaviour
    {
        [Header("Rank")]
        [SerializeField] private TMP_Text rankText;
        [SerializeField] private Image    rankMedalIcon;   // optional gold/silver/bronze icon
        [SerializeField] private Sprite   goldSprite;
        [SerializeField] private Sprite   silverSprite;
        [SerializeField] private Sprite   bronzeSprite;

        [Header("Player Info")]
        [SerializeField] private Image    avatarImage;
        [SerializeField] private Sprite   defaultAvatar;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text scoreText;

        [Header("Highlight (current player)")]
        [SerializeField] private GameObject highlightBackground;

        public LeaderboardEntry Data { get; private set; }

        private Coroutine _avatarLoadRoutine;

        /// <summary>
        /// Populates this row. prevRank should be the rank this same uid had
        /// on the previous update (pass the same value as newRank on first spawn
        /// so it doesn't animate on initial population).
        /// </summary>
        public void Setup(LeaderboardEntry entry, bool isCurrentPlayer, int prevRank)
        {
            Data = entry;

            if (nameText != null)  nameText.text  = entry.displayName;
            if (scoreText != null) scoreText.text = entry.totalScore.ToString("N0");
            if (highlightBackground != null) highlightBackground.SetActive(isCurrentPlayer);

            SetRankVisual(entry.rank);

            if (avatarImage != null)
            {
                if (_avatarLoadRoutine != null) { StopCoroutine(_avatarLoadRoutine); _avatarLoadRoutine = null; }

                if (!string.IsNullOrEmpty(entry.avatarId))
                {
                    // Preset avatar — same resolution ProfileManager/ProfilePanel use.
                    // Local + instant, no network needed, and takes priority over avatarUrl.
                    var preset = Resources.Load<AvatarPresetData>("Avatars/" + entry.avatarId);
                    if (preset != null && preset.sprite != null)
                        avatarImage.sprite = preset.sprite;
                    else if (defaultAvatar != null)
                        avatarImage.sprite = defaultAvatar;
                }
                else if (!string.IsNullOrEmpty(entry.avatarUrl))
                {
                    _avatarLoadRoutine = StartCoroutine(LoadAvatarCoroutine(entry.avatarUrl));
                }
                else if (defaultAvatar != null)
                {
                    avatarImage.sprite = defaultAvatar;
                }
            }

            if (prevRank != entry.rank)
                AnimateRankChange(prevRank, entry.rank);
        }

        private void SetRankVisual(int rank)
        {
            if (rankText != null) rankText.text = rank.ToString();

            if (rankMedalIcon == null) return;

            switch (rank)
            {
                case 1: rankMedalIcon.gameObject.SetActive(true); rankMedalIcon.sprite = goldSprite;   break;
                case 2: rankMedalIcon.gameObject.SetActive(true); rankMedalIcon.sprite = silverSprite; break;
                case 3: rankMedalIcon.gameObject.SetActive(true); rankMedalIcon.sprite = bronzeSprite; break;
                default: rankMedalIcon.gameObject.SetActive(false); break;
            }
        }

        /// <summary>Slides the rank number up (moved up the board, green flash)
        /// or down (dropped, red flash) using DOTween — matches the DOTween
        /// convention already used across the board/pet systems.</summary>
        private void AnimateRankChange(int prevRank, int newRank)
        {
            if (rankText == null) return;

            bool movedUp = newRank < prevRank;
            float startOffsetY = movedUp ? -18f : 18f;

            rankText.transform.DOKill();
            rankText.rectTransform.anchoredPosition = new Vector2(rankText.rectTransform.anchoredPosition.x, startOffsetY);
            rankText.rectTransform.DOAnchorPosY(0f, 0.35f).SetEase(Ease.OutBack);

            Color flashColor = movedUp ? new Color(0.35f, 0.85f, 0.35f) : new Color(0.9f, 0.35f, 0.35f);
            Color originalColor = rankText.color;
            rankText.DOColor(flashColor, 0.15f).OnComplete(() =>
            {
                rankText.DOColor(originalColor, 0.5f);
            });
        }

        private IEnumerator LoadAvatarCoroutine(string url)
        {
            using UnityWebRequest req = UnityWebRequestTexture.GetTexture(url);
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                if (defaultAvatar != null) avatarImage.sprite = defaultAvatar;
                yield break;
            }

            Texture2D tex = DownloadHandlerTexture.GetContent(req);
            avatarImage.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }

        private void OnDestroy()
        {
            rankText?.transform.DOKill();
        }
    }
}