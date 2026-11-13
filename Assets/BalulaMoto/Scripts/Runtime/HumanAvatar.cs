// BALULA MOTO — composant des personnages réalistes (prefabs Rocketbox générés par HumanBuilder).
// Dans son propre fichier : Unity exige que le nom du fichier corresponde à la classe pour les composants sauvegardés.
using UnityEngine;

namespace BalulaMoto.Runtime
{
    /// <summary>Composant présent sur chaque prefab de personnage réaliste.</summary>
    public class HumanAvatar : MonoBehaviour
    {
        public static readonly string[] StateNames = { "Idle", "Idle2", "Walk", "WalkSlow", "Wave", "Sit", "Phone", "Talk", "Wait", "Umbrella", "Crouch" };
        private static int[] hashes;

        public string AvatarId;
        public string Category;
        public bool Female;
        public bool Child;
        public Animator Anim;
        public Transform Model;
        public Transform Bip01, Pelvis, Spine1, Spine2, Head;
        public Transform UpperArmL, ForearmL, HandL, UpperArmR, ForearmR, HandR;
        public Transform ThighL, CalfL, FootL, ThighR, CalfR, FootR;
        /// <summary>Vitesse (m/s) à laquelle l'animation de marche ne glisse pas.</summary>
        public float WalkCycleSpeed = 1.35f;
        /// <summary>Hauteur approximative du sommet du crâne au repos (m).</summary>
        public float HeadTopHeight = 1.72f;

        private int current = -1;
        private int pending = -1;
        private bool pendingRandom;

        public HumanAction Current { get { return current < 0 ? HumanAction.Idle : (HumanAction)current; } }

        private static void InitHashes()
        {
            if (hashes != null) return;
            hashes = new int[StateNames.Length];
            for (int i = 0; i < StateNames.Length; i++) hashes[i] = Animator.StringToHash(StateNames[i]);
        }

        /// <summary>Lance une animation (fondu enchaîné). randomStart : démarre à un instant aléatoire du cycle.</summary>
        public void Play(HumanAction action, float fade = 0.25f, bool randomStart = false)
        {
            if (Anim == null) return;
            int a = (int)action;
            if (a == current && pending < 0) return;
            if (!Anim.isActiveAndEnabled || !Anim.gameObject.activeInHierarchy)
            {
                pending = a;
                pendingRandom = randomStart;
                return;
            }
            InitHashes();
            int h = hashes[a];
            if (!Anim.HasState(0, h))
            {
                a = 0;
                h = hashes[0];
                if (a == current) return;
            }
            bool first = current < 0;
            current = a;
            pending = -1;
            if (first || fade <= 0f) Anim.Play(h, 0, randomStart ? Random.value : 0f);
            else Anim.CrossFadeInFixedTime(h, fade, 0, randomStart ? Random.value * 2f : 0f);
        }

        private void Update()
        {
            if (pending >= 0 && Anim != null && Anim.isActiveAndEnabled)
            {
                int p = pending;
                pending = -1;
                current = -1;
                Play((HumanAction)p, 0f, pendingRandom);
            }
        }

        public void SetPlaybackSpeed(float speed)
        {
            if (Anim != null) Anim.speed = speed;
        }

        /// <summary>Fige la pose actuelle (conducteurs de la circulation) : plus aucun calcul d'animation.</summary>
        public void Freeze()
        {
            if (Anim != null) Anim.enabled = false;
        }

        public Vector3 HeadTopWorld()
        {
            float s = transform.lossyScale.y;
            if (Head != null) return Head.position + transform.up * (0.21f * s * (Child ? 0.85f : 1f));
            return transform.position + transform.up * HeadTopHeight * s;
        }
    }
}
