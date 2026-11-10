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
    private static readonly int HitHash = Animator.StringToHash("hit");
    private static readonly int BlockHash = Animator.StringToHash("block");

    private void Awake()
    {
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (fighter == null) fighter = GetComponentInParent<Fighter>();
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

        bool attacking = fighter.IsAttacking;
        animator.SetBool(IsAttackingHash, attacking);
        if (spriteRenderer != null)
            spriteRenderer.sortingOrder = attacking ? attackSortingOrder : idleSortingOrder;

        // A new attack started: jump straight to the start of its clip
        if (fighter.AttackSerial != lastAttackSerial)
        {
            lastAttackSerial = fighter.AttackSerial;
            AttackData atk = fighter.CurrentAttack;
            if (atk != null && !string.IsNullOrEmpty(atk.animationState))
                animator.Play(Animator.StringToHash(atk.animationState), 0, 0f);
        }
    }

    // Restart the stun clip on every hit, so a second hit during stun plays it again
    private void OnHitReceived(BaseFighter.HitType type, AttackData atk)
    {
        if (type == BaseFighter.HitType.None) return;
        animator.Play(type == BaseFighter.HitType.Blocked ? BlockHash : HitHash, 0, 0f);
    }
}
