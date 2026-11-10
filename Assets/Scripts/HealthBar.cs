using UnityEngine;
using UnityEngine.UI;

// Shows a fighter's Health as a filled bar, with a lighter "recent damage" bar that catches up after a short delay.
// Both Images must be Image Type = Filled, Horizontal. Set Fill Origin = Right to mirror the bar for the right side.
public class HealthBar : MonoBehaviour
{
    [SerializeField] private Health target;
    [SerializeField] private Image fill;
    [SerializeField] private Image damageTrail;
    [Tooltip("Optional: shows the character's portrait")]
    [SerializeField] private Image portrait;

    [Header("damage trail")]
    [SerializeField] private float trailDelay = 0.4f;      // seconds before the trail starts sliding down
    [SerializeField] private float slideDuration = 0.5f;   // seconds the slide takes, whatever the damage

    private float lastPct = 1f;
    private float delayTimer;
    private float slideSpeed;   // fill per second, worked out when the slide starts

    private void Awake()
    {
        // fillAmount only does anything on a Filled image, so make sure the fill is one (keeps its Fill Origin)
        if (fill != null)
        {
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
        }

        if (damageTrail == null)
        {
            Debug.LogWarning(name + ": HealthBar has no Damage Trail assigned");
            return;
        }
        // The trail has to be a Filled image going the same way as the fill, or fillAmount does nothing
        damageTrail.type = Image.Type.Filled;
        if (fill != null)
        {
            damageTrail.fillMethod = fill.fillMethod;
            damageTrail.fillOrigin = fill.fillOrigin;
        }
    }

    // MatchSetup calls this once the fighters are spawned
    public void SetTarget(Health health, Sprite portraitSprite)
    {
        target = health;
        lastPct = 1f;
        delayTimer = 0f;
        if (fill != null) fill.fillAmount = 1f;
        if (damageTrail != null) damageTrail.fillAmount = 1f;
        if (portrait != null && portraitSprite != null) portrait.sprite = portraitSprite;
    }

    private void Update()
    {
        if (target == null || target.Max <= 0) return;

        float pct = target.Current / (float)target.Max;
        if (fill != null) fill.fillAmount = pct;

        if (pct < lastPct) delayTimer = trailDelay;   // took damage, hold the trail for a moment

        if (damageTrail != null)
        {
            if (pct > damageTrail.fillAmount)
            {
                damageTrail.fillAmount = pct;   // healed or round reset, no trail going up
            }
            else if (delayTimer > 0f)
            {
                delayTimer -= Time.deltaTime;
                // Speed is set so the whole gap closes in slideDuration
                slideSpeed = (damageTrail.fillAmount - pct) / Mathf.Max(0.01f, slideDuration);
            }
            else
            {
                damageTrail.fillAmount = Mathf.MoveTowards(damageTrail.fillAmount, pct, slideSpeed * Time.deltaTime);
            }
        }

        lastPct = pct;
    }
}
