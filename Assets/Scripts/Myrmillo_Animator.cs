using UnityEngine;

[RequireComponent(typeof(Animator))]
public class Myrmillo_Animator : MonoBehaviour
{
    [SerializeField] private Myrmillo_Behavior movement;
    private Animator animator;
    private bool wasAttacking;
    private static readonly int MoveDirHash = Animator.StringToHash("MoveDir");
    private static readonly int IsAttackingHash = Animator.StringToHash("IsAttacking");
    private static readonly int StandingLightHash = Animator.StringToHash("standing_light");

    private void Awake() => animator = GetComponent<Animator>();

    private void Update()
    {
        animator.SetFloat(MoveDirHash, movement.MoveDirRelative);

        bool attacking = movement.IsAttacking;
        animator.SetBool(IsAttackingHash, attacking);

        // The frame the attack begins, jump straight to the start of the attack clip
        if (attacking && !wasAttacking)
            animator.Play(StandingLightHash, 0, 0f);

        wasAttacking = attacking;
    }
}