using UnityEngine;
using UnityEngine.Serialization;

// Shows a Fighter's state on its Animator. Only reads the fighter, never changes it.
[RequireComponent(typeof(Animator))]
public class FighterAnimator : MonoBehaviour
{
    [FormerlySerializedAs("movement")]
    [SerializeField] private Fighter fighter;
    [Header("draw order")]
    [SerializeField] private int idleSortingOrder = 1;
    [SerializeField] private int attackSortingOrder = 2;   // drawn over the opponent while attacking
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private int lastAttackSerial;
    private static readonly int MoveDirHash = Animator.StringToHash("MoveDir");
    private static readonly int IsAttackingHash = Animator.StringToHash("IsAttacking");
    private static readonly int IsHitstunHash = Animator.StringToHash("IsHitstun");
    private static readonly int IsBlockstunHash = Animator.StringToHash("IsBlockstun");
    private static readonly int IsCrouchingHash = Animator.StringToHash("IsCrouching");
    private static readonly int HitHash = Animator.StringToHash("hit");
    private static readonly int BlockHash = Animator.StringToHash("block");
    private static readonly int CrouchHitHash = Animator.StringToHash("crouch_hit");
    private static readonly int CrouchBlockHash = Animator.StringToHash("crouch_block");
    private static readonly int IdleHash = Animator.StringToHash("idle");
    private static readonly int CrouchingHash = Animator.StringToHash("crouching");
    private static readonly int KnockdownHash = Animator.StringToHash("knockdown");
    private static readonly int DownHash = Animator.StringToHash("down");
    private static readonly int GetUpHash = Animator.StringToHash("getup");
    private bool hasCrouchParam;   // the controller may not have IsCrouching yet
    private int lastAttackStateHash;   // Animator state of the most recent attack
    private BaseFighter.KnockdownPhase lastKnockdownPhase;
    private string warnedMissingState;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (fighter == null) fighter = GetComponentInParent<Fighter>();
        foreach (AnimatorControllerParameter p in animator.parameters)
            if (p.nameHash == IsCrouchingHash && p.type == AnimatorControllerParameterType.Bool) hasCrouchParam = true;
    }

    private void OnEnable()
    {
        if (fighter != null) fighter.HitReceived += OnHitReceived;
    }

    private void OnDisable()
    {
        if (fighter != null) fighter.HitReceived -= OnHitReceived;
    }

    private void Update()
    {
        if (fighter == null) return;
        animator.SetFloat(MoveDirHash, fighter.MoveDirRelative);
        animator.SetBool(IsHitstunHash, fighter.IsHitstun);
        animator.SetBool(IsBlockstunHash, fighter.IsBlockstun);
        if (hasCrouchParam) animator.SetBool(IsCrouchingHash, fighter.IsCrouching);

        bool attacking = fighter.IsAttacking;
        animator.SetBool(IsAttackingHash, attacking);
        if (spriteRenderer != null)
            spriteRenderer.sortingOrder = attacking ? attackSortingOrder : idleSortingOrder;

        // Knockdown: play "knockdown" (falling), then "down" (lying), then "getup", each when its phase starts
        BaseFighter.KnockdownPhase kd = fighter.CurrentKnockdownPhase;
        if (kd != lastKnockdownPhase)
        {
            lastKnockdownPhase = kd;
            if (kd != BaseFighter.KnockdownPhase.None) PlayKnockdown(kd);
        }

        // A new attack started: jump straight to the start of its clip
        if (fighter.AttackSerial != lastAttackSerial)
        {
            lastAttackSerial = fighter.AttackSerial;
            AttackData atk = fighter.CurrentAttack;
            if (atk != null && !string.IsNullOrEmpty(atk.animationState))
            {
                int hash = Animator.StringToHash(atk.animationState);
                if (animator.HasState(0, hash))
                {
                    lastAttackStateHash = hash;
                    animator.Play(hash, 0, 0f);
                }
                else WarnMissing(atk.animationState);
            }
        }

        UpdatePosture();
    }

    // Plays the clip for a knockdown phase. Until the new clips exist it falls back to ones that do.
    private void PlayKnockdown(BaseFighter.KnockdownPhase phase)
    {
        int[] tryStates;
        switch (phase)
        {
            case BaseFighter.KnockdownPhase.Fall: tryStates = new[] { KnockdownHash, HitHash }; break;
            case BaseFighter.KnockdownPhase.Down: tryStates = new[] { DownHash, CrouchHitHash, CrouchingHash }; break;
            default: tryStates = new[] { GetUpHash, CrouchingHash }; break;
        }
        foreach (int s in tryStates)
            if (animator.HasState(0, s)) { animator.Play(s, 0, 0f); return; }
    }

    private void WarnMissing(string state)
    {
        if (warnedMissingState == state) return;
        warnedMissingState = state;
        Debug.LogWarning(name + ": the Animator has no state called \"" + state + "\"");
    }

    // Crouching takes priority over the Animator's own transitions, so it shows on the tick it happens:
    // - free (not attacking or stunned) and crouching → "crouching" right away, even mid-walk
    // - free and standing again after a crouch pose → "idle" (walking then follows from idle as usual)
    // - in blockstun → the block pose that matches the current posture (it can be switched during blockstun)
    // Attacks, hits and blocks are started elsewhere (attack serial, OnHitReceived) and take over from this.
    private void UpdatePosture()
    {
        if (fighter.IsKnockedDown) return;   // PlayKnockdown handles it
        bool crouching = fighter.IsCrouching;

        if (fighter.IsBlockstun)
        {
            int want = crouching ? CrouchBlockHash : BlockHash;
            if (!InState(want) && animator.HasState(0, want)) animator.Play(want, 0, 0f);
            return;
        }
        if (fighter.IsAttacking || fighter.IsStunned) return;

        if (crouching)
        {
            if (!InState(CrouchingHash) && animator.HasState(0, CrouchingHash)) animator.Play(CrouchingHash, 0, 0f);
        }
        else if (InLeftoverPose() && animator.HasState(0, IdleHash))
        {
            animator.Play(IdleHash, 0, 0f);
        }
    }

    // Is the Animator in (or blending into) this state?
    private bool InState(int hash)
    {
        if (animator.GetCurrentAnimatorStateInfo(0).shortNameHash == hash) return true;
        return animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).shortNameHash == hash;
    }

    // Still showing a pose that no longer applies: a crouch state after standing up,
    // or the last attack's clip after the attack ended (so the fighter doesn't wait for the clip's exit time)
    private bool InLeftoverPose()
    {
        if (animator.IsInTransition(0)) return false;   // already on its way somewhere
        int s = animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
        return s == CrouchingHash || s == CrouchBlockHash || s == CrouchHitHash ||
               s == HitHash || s == BlockHash ||
               s == KnockdownHash || s == DownHash || s == GetUpHash ||
               (lastAttackStateHash != 0 && s == lastAttackStateHash);
    }

    // Restart the stun clip on every hit, so a second hit during stun plays it again.
    // While crouching it plays "crouch_block" / "crouch_hit" if the Animator has them, otherwise the standing ones.
    private void OnHitReceived(BaseFighter.HitType type, AttackData atk)
    {
        if (type == BaseFighter.HitType.None || fighter.IsKnockedDown) return;   // a knockdown plays its own clips
        bool blocked = type == BaseFighter.HitType.Blocked;
        int state = blocked ? BlockHash : HitHash;
        if (fighter.IsCrouching)
        {
            int crouchState = blocked ? CrouchBlockHash : CrouchHitHash;
            if (animator.HasState(0, crouchState)) state = crouchState;
        }
        animator.Play(state, 0, 0f);
    }
}
