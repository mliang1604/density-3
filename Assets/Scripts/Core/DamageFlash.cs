using System.Collections;
using UnityEngine;

namespace Density3.Core
{
    /// <summary>Flashes all child renderers white briefly when damaged.</summary>
    [RequireComponent(typeof(Health))]
    public class DamageFlash : MonoBehaviour
    {
        private Health health;
        private Renderer[] renderers;
        private Color[] originals;
        private Coroutine routine;

        private void Awake()
        {
            health = GetComponent<Health>();
            renderers = GetComponentsInChildren<Renderer>();
            originals = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
                originals[i] = renderers[i].material.color;
            health.Damaged += OnDamaged;
        }

        private void OnDamaged(DamageInfo info)
        {
            if (!isActiveAndEnabled) return;
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(Flash());
        }

        private IEnumerator Flash()
        {
            SetColor(Color.white);
            yield return new WaitForSeconds(0.07f);
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) renderers[i].material.color = originals[i];
        }

        private void SetColor(Color c)
        {
            foreach (var r in renderers)
                if (r != null) r.material.color = c;
        }

        private void OnDestroy()
        {
            if (health != null) health.Damaged -= OnDamaged;
        }
    }
}
