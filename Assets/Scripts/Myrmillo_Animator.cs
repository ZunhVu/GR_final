using UnityEngine;

[RequireComponent(typeof(Animator))]
public class Myrmillo_Animator : MonoBehaviour
{
    [SerializeField] private Myrmillo_Behavior movement;
    private Animator animator;
    private bool wasAttacking;
    private static readonly int MoveDirHash = Animator.StringToHash("MoveDir");
    private static readonly int IsAttackingHash = Animator.StringToHash("IsAttacking");
    private static readonly int IsHitstunHash = Animator.StringToHash("IsHitstun");
    private static readonly int IsBlockstunHash = Animator.StringToHash("IsBlockstun");
    private static readonly int StandingLightHash = Animator.StringToHash("standing_light");
    private static readonly int HitHash = Animator.StringToHash("hit");
    private static readonly int BlockHash = Animator.StringToHash("block");

    private void Awake() => animator = GetComponent<Animator>();

    private void OnEnable()
    {
        if (movement != null) movement.HitReceived += OnHitReceived;
    }

    private void OnDisable()
    {
        if (movement != null) movement.HitReceived -= OnHitReceived;
    }

    private void Update()
    {
        animator.SetFloat(MoveDirHash, movement.MoveDirRelative);
        animator.SetBool(IsHitstunHash, movement.IsHitstun);
        animator.SetBool(IsBlockstunHash, movement.IsBlockstun);

        bool attacking = movement.IsAttacking;
        animator.SetBool(IsAttackingHash, attacking);

        // The frame the attack begins, jump straight to the start of the attack clip
        if (attacking && !wasAttacking)
            animator.Play(StandingLightHash, 0, 0f);

        wasAttacking = attacking;
    }

    // Restart the stun clip on every hit, so a second hit during stun plays it again
    private void OnHitReceived(BaseFighter.HitType type, AttackData atk)
    {
        if (type == BaseFighter.HitType.None) return;
        animator.Play(type == BaseFighter.HitType.Blocked ? BlockHash : HitHash, 0, 0f);
        wasAttacking = false;   // a hit cancels the attack, so the next attack restarts its clip
    }
}
