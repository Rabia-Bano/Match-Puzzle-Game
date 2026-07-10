// ============================================================
//  PetSystem.cs  —  MonoBehaviour
//
//  Manages the pet companion:
//    • HP bar (shown above bottom panel)
//    • Pet skill activation (tap pet button)
//    • HP decreases when player makes bad moves or boss attacks
//    • HP increases when player makes chain combos
//
//  Pet Skills (based on PetSkillType):
//    Heal       — restores pet HP
//    ClearRow   — clears a random row
//    AddMoves   — gives +3 moves
//    ColorClear — clears most common color
//
//  Attach to: PetSystem (empty GameObject)
//  Wire: boardGrid, moveCounter, specialActivator
// ============================================================

using System;
using System.Collections;
using UnityEngine;
using DG.Tweening;

namespace Match3
{
    public enum PetSkillType
    {
        Heal       = 0,
        ClearRow   = 1,
        AddMoves   = 2,
        ColorClear = 3
    }

    [CreateAssetMenu(fileName = "PetData_New", menuName = "Match3/Pet Data", order = 5)]
    public class PetData : ScriptableObject
    {
        public string       petName;
        public Sprite       petSprite;
        public Sprite       petAvatarSprite;
        public int          maxHP       = 100;
        public PetSkillType skillType   = PetSkillType.Heal;
        public int          skillValue  = 20;    // heal amount / rows / moves / etc.
        public int          skillCooldownMoves = 5;  // moves between skill uses
        [TextArea] public string skillDescription;
    }

    public class PetSystem : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────

        [Header("Pet Configuration")]
        [SerializeField] private PetData    activePet;
        [SerializeField] private BoardGrid  boardGrid;
        [SerializeField] private MoveCounter moveCounter;

        [Header("HP Settings")]
        [SerializeField] private int hpLossPerBadMove = 0;   // 0 = no loss by default
        [SerializeField] private int hpGainPerCombo   = 5;   // gained on 4+ match

        // ── Events ────────────────────────────────────────────

        /// <summary>Fires when HP changes. (currentHP, maxHP)</summary>
        public event Action<int, int> OnHPChanged;

        /// <summary>Fires when skill activates.</summary>
        public event Action<PetSkillType> OnSkillUsed;

        // ── Public state ──────────────────────────────────────

        public int    CurrentHP   { get; private set; }
        public int    MaxHP       => activePet != null ? activePet.maxHP : 100;
        public Sprite PetSprite   => activePet?.petAvatarSprite ?? activePet?.petSprite;
        public bool   IsAlive     => CurrentHP > 0;
        public float  HPFraction  => MaxHP > 0 ? (float)CurrentHP / MaxHP : 0f;

        public bool   SkillReady  => _movesSinceLastSkill >= (activePet?.skillCooldownMoves ?? 5);
        public int    SkillCooldownRemaining =>
            Mathf.Max(0, (activePet?.skillCooldownMoves ?? 5) - _movesSinceLastSkill);

        private int   _movesSinceLastSkill = 0;

        // ── Lifecycle ─────────────────────────────────────────

        private void Start()
        {
            if (activePet == null)
            {
                Debug.LogWarning("[PetSystem] No PetData assigned. Pet disabled.");
                return;
            }

            CurrentHP = activePet.maxHP;
            OnHPChanged?.Invoke(CurrentHP, MaxHP);

            // Listen for moves
            if (moveCounter != null)
                moveCounter.OnMovesChanged.AddListener(_ => _movesSinceLastSkill++);
        }

        // ── Public API ────────────────────────────────────────

        /// <summary>
        /// Called when player taps the PET BUTTON in the bottom panel.
        /// If skill is ready, activates it.
        /// </summary>
        public void OnPetButtonTapped()
        {
            if (activePet == null) return;

            if (!SkillReady)
            {
                Debug.Log($"[PetSystem] Skill not ready. {SkillCooldownRemaining} moves left.");
                return;
            }

            StartCoroutine(ActivateSkill());
        }

        /// <summary>Called by BoardController when a 4+ match cascade happens.</summary>
        public void OnComboCleared(int comboSize)
        {
            if (comboSize >= 4)
                HealHP(hpGainPerCombo * (comboSize - 3));
        }

        public void TakeDamage(int amount)
        {
            CurrentHP = Mathf.Max(0, CurrentHP - amount);
            OnHPChanged?.Invoke(CurrentHP, MaxHP);

            if (CurrentHP <= 0)
                Debug.Log("[PetSystem] Pet has fainted!");
        }

        public void HealHP(int amount)
        {
            CurrentHP = Mathf.Min(MaxHP, CurrentHP + amount);
            OnHPChanged?.Invoke(CurrentHP, MaxHP);
        }

        // ── Skill activation ──────────────────────────────────

        private IEnumerator ActivateSkill()
        {
            _movesSinceLastSkill = 0;
            OnSkillUsed?.Invoke(activePet.skillType);

            Debug.Log($"[PetSystem] Skill used: {activePet.skillType}");

            switch (activePet.skillType)
            {
                case PetSkillType.Heal:
                    HealHP(activePet.skillValue);
                    break;

                case PetSkillType.AddMoves:
                    moveCounter?.AddMoves(activePet.skillValue);
                    break;

                case PetSkillType.ClearRow:
                    yield return StartCoroutine(ClearRandomRow());
                    break;

                case PetSkillType.ColorClear:
                    // Fires event — BoardController / SpecialTileActivator handles it
                    Debug.Log("[PetSystem] ColorClear skill — wire to SpecialTileActivator.");
                    break;
            }
        }

        private IEnumerator ClearRandomRow()
        {
            if (boardGrid == null) yield break;

            int randomRow = UnityEngine.Random.Range(0, boardGrid.Height);
            var list = new System.Collections.Generic.List<Tile>();

            for (int x = 0; x < boardGrid.Width; x++)
            {
                Tile t = boardGrid.GetTile(x, randomRow);
                if (t != null) list.Add(t);
            }

            foreach (var tile in list)
            {
                if (tile == null) continue;
                if (boardGrid.GetTile(tile.GridX, tile.GridY) != tile) continue;

                tile.SetState(TileState.Matched);
                boardGrid.RemoveTile(tile.GridX, tile.GridY);

                tile.transform.DOScale(Vector3.zero, 0.12f)
                    .SetEase(Ease.InBack)
                    .OnComplete(() => tile.transform.localScale = Vector3.one);

                yield return new WaitForSeconds(0.04f);
            }

            yield return new WaitForSeconds(0.2f);
        }
    }
}
