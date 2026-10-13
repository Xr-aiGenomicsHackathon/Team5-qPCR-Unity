using System;
using UnityEngine;

namespace Team5.qPCR
{
    /// <summary>Swaps each controller-driven hand between bare-skin and glove materials.</summary>
    public sealed class HandAppearanceController : MonoBehaviour
    {
        [SerializeField] private Renderer[] leftHandRenderers = Array.Empty<Renderer>();
        [SerializeField] private Renderer[] rightHandRenderers = Array.Empty<Renderer>();
        [SerializeField] private Material bareHandMaterial;
        [SerializeField] private Material blueGloveMaterial;

        private Material[][] cachedLeftMaterials;
        private Material[][] cachedRightMaterials;

        private void Awake()
        {
            cachedLeftMaterials = CacheMaterials(leftHandRenderers);
            cachedRightMaterials = CacheMaterials(rightHandRenderers);
        }

        public void SetGloved(PPEHandSide side, bool gloved)
        {
            var renderers = side == PPEHandSide.Left ? leftHandRenderers : rightHandRenderers;
            var cached = side == PPEHandSide.Left ? cachedLeftMaterials : cachedRightMaterials;
            Apply(renderers, cached, gloved);
        }

        public void ResetHands()
        {
            SetGloved(PPEHandSide.Left, false);
            SetGloved(PPEHandSide.Right, false);
        }

        private void Apply(Renderer[] renderers, Material[][] cached, bool gloved)
        {
            if (renderers == null) return;
            for (var i = 0; i < renderers.Length; i++)
            {
                var target = renderers[i];
                if (target == null) continue;

                if (!gloved && bareHandMaterial == null && cached != null && i < cached.Length)
                {
                    target.sharedMaterials = cached[i];
                    continue;
                }

                var material = gloved ? blueGloveMaterial : bareHandMaterial;
                if (material == null) continue;
                var current = target.sharedMaterials;
                for (var slot = 0; slot < current.Length; slot++) current[slot] = material;
                target.sharedMaterials = current;
            }
        }

        private static Material[][] CacheMaterials(Renderer[] renderers)
        {
            if (renderers == null) return Array.Empty<Material[]>();
            var result = new Material[renderers.Length][];
            for (var i = 0; i < renderers.Length; i++)
                result[i] = renderers[i] == null ? Array.Empty<Material>() : renderers[i].sharedMaterials;
            return result;
        }
    }
}
