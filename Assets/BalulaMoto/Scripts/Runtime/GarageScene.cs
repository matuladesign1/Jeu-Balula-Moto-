// BALULA MOTO — garage du menu principal (style « garage » des jeux de moto donnés en exemple) :
// murs de briques, rideau métallique, établi, pneus, fûts, affiches, néons ; la moto du joueur au centre et
// son chauffeur debout à côté. Construit à l'écart de la carte ; la caméra du menu le filme en plan fixe.
using BalulaMoto.Core;
using UnityEngine;

namespace BalulaMoto.Runtime
{
    public class GarageScene : MonoBehaviour
    {
        public static GarageScene Instance;
        public static readonly Vector3 Origin = new Vector3(-7000f, 0f, -7000f);

        private Transform motoSlot, riderSlot, turntable, tableSlot;
        private GameObject moto, rider, shown;
        private string motoKey, riderKey, shownKey;
        private bool showroom;

        public Vector3 CameraPosition { get { return Origin + new Vector3(-0.9f, 1.55f, -3.5f); } }
        public Vector3 CameraLook { get { return Origin + new Vector3(-0.15f, 0.95f, 0.8f); } }
        /// <summary>Garage du menu (sélection du véhicule) : le plateau tournant au centre, la fiche à gauche de l'écran.</summary>
        public Vector3 ShowroomCameraPosition { get { return Origin + new Vector3(-1.05f, 1.6f, -3.4f); } }
        public Vector3 ShowroomCameraLook { get { return Origin + new Vector3(-0.55f, 0.62f, 1.0f); } }

        /// <summary>Cadrage du garage selon le véhicule : plus près pour une moto, un peu moins pour un tricycle.</summary>
        public void ShowroomShot(VehicleDef def, out Vector3 pos, out Vector3 look)
        {
            float k = def != null && def.IsMoto ? 0.64f : 0.86f;
            Vector3 t = Origin + new Vector3(0.45f, 0.5f, 1.0f);
            pos = t + (ShowroomCameraPosition - t) * k;
            look = t + (ShowroomCameraLook - t) * k;
        }

        /// <summary>Cadrage du chauffeur debout à côté du plateau (onglet CHAUFFEUR).</summary>
        public void DriverShot(out Vector3 pos, out Vector3 look)
        {
            pos = Origin + new Vector3(0.75f, 1.45f, -2.3f);
            look = Origin + new Vector3(1.55f, 1.0f, 0.15f);
        }

        public static GarageScene Create()
        {
            GameObject g = new GameObject("GarageMenu");
            g.transform.position = Origin;
            Instance = g.AddComponent<GarageScene>();
            Instance.Build();
            return Instance;
        }

        private static Material TexMat(string name, Color tint, Vector2 tiling, float smooth)
        {
            Texture2D tex = Resources.Load<Texture2D>("BalulaMoto/Garage/" + name);
            Material m = new Material(Mats.Lit(tint, smooth));
            m.color = tint;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint);
            if (tex != null)
            {
                m.mainTexture = tex;
                m.mainTextureScale = tiling;
                if (m.HasProperty("_BaseMap")) { m.SetTexture("_BaseMap", tex); m.SetTextureScale("_BaseMap", tiling); }
            }
            return m;
        }

        private static GameObject Slab(Transform parent, string name, Vector3 pos, Vector3 size, Material mat)
        {
            GameObject g = Prims.Box(parent, name, pos, size, Color.white);
            g.GetComponent<Renderer>().sharedMaterial = mat;
            return g;
        }

        private static void Poster(Transform parent, string res, Vector3 pos, float yaw, float w)
        {
            Texture2D tex = Resources.Load<Texture2D>(res);
            if (tex == null) return;
            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.Destroy(g.GetComponent<Collider>());
            g.name = "Affiche";
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            // Un quad Unity est visible du côté -Z local : on le tourne pour qu'il regarde vers l'intérieur
            g.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            g.transform.localScale = new Vector3(w, w * tex.height / Mathf.Max(1, tex.width), 1f);
            Material m = new Material(Mats.Lit(Color.white, 0.3f));
            m.color = Color.white;
            m.mainTexture = tex;
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            g.GetComponent<Renderer>().sharedMaterial = m;
        }

        private void Build()
        {
            Transform t = transform;
            Material floor = TexMat("sol", new Color(0.85f, 0.85f, 0.83f), new Vector2(4f, 3.4f), 0.25f);
            Material bricks = TexMat("briques", Color.white, new Vector2(12.5f, 5f), 0.08f);
            Material plasterTop = TexMat("crepi", new Color(0.93f, 0.88f, 0.72f), new Vector2(5f, 2f), 0.05f);
            Material plasterLow = TexMat("crepi", new Color(0.16f, 0.42f, 0.3f), new Vector2(5f, 1f), 0.1f);
            Material shutter = TexMat("tole", new Color(0.82f, 0.85f, 0.9f), new Vector2(1f, 3f), 0.45f);

            // Sol, mur du fond (briques), murs latéraux (enduit bicolore)
            Slab(t, "Sol", new Vector3(0f, -0.1f, 1f), new Vector3(12f, 0.2f, 10f), floor);
            Slab(t, "MurFond", new Vector3(0f, 2.25f, 4.1f), new Vector3(12f, 4.5f, 0.2f), bricks);
            foreach (float sx in new[] { -6.1f, 6.1f })
            {
                Slab(t, "MurHaut", new Vector3(sx, 2.25f, 1f), new Vector3(0.2f, 4.5f, 10f), plasterTop);
                Slab(t, "MurBas", new Vector3(sx > 0f ? sx - 0.11f : sx + 0.11f, 0.6f, 1f), new Vector3(0.04f, 1.2f, 10f), plasterLow);
            }
            // Rideau métallique et enseigne
            Slab(t, "Rideau", new Vector3(-1.6f, 1.85f, 3.98f), new Vector3(4.2f, 3.7f, 0.05f), shutter);
            Prims.Box(t, "Linteau", new Vector3(-1.6f, 3.78f, 3.96f), new Vector3(4.5f, 0.18f, 0.12f), new Color(0.25f, 0.25f, 0.27f));
            GameObject sign = Prims.Box(t, "Enseigne", new Vector3(-1.6f, 4.15f, 3.95f), new Vector3(3.6f, 0.55f, 0.06f), new Color(0.98f, 0.78f, 0.05f));
            sign.GetComponent<Renderer>().sharedMaterial = Mats.Unlit(new Color(0.98f, 0.78f, 0.05f));
            GameObject label = Prims.Label(t, "GARAGE MATULA", new Vector3(-1.6f, 4.15f, 3.9f), 0.07f, new Color(0.08f, 0.08f, 0.1f), false);
            label.name = "TexteEnseigne";

            // Marquage au sol
            Material yellow = Mats.Lit(new Color(0.95f, 0.75f, 0.05f), 0.3f);
            foreach (float lx in new[] { -1.7f, 2.9f })
                Prims.Box(t, "Ligne", new Vector3(lx, 0.005f, 1.2f), new Vector3(0.1f, 0.01f, 4.6f), Color.white).GetComponent<Renderer>().sharedMaterial = yellow;

            // Affiches publicitaires (marques fictives du jeu)
            Poster(t, "BalulaMoto/Pubs/pub_0", new Vector3(-5.97f, 2.5f, 2.2f), -90f, 2.6f);
            Poster(t, "BalulaMoto/Pubs/pub_1", new Vector3(2.4f, 3.0f, 3.98f), 0f, 2.4f);
            Poster(t, "BalulaMoto/Pubs/pub_10", new Vector3(5.97f, 2.5f, 1.6f), 90f, 2.4f);
            Poster(t, "BalulaMoto/Pubs/pub_5", new Vector3(5.97f, 2.5f, -1.4f), 90f, 2.2f);

            // Établi et outils
            Color wood = new Color(0.45f, 0.3f, 0.18f);
            Prims.Box(t, "Etabli", new Vector3(4.3f, 0.86f, 3.45f), new Vector3(2.8f, 0.08f, 0.9f), wood);
            foreach (float lx in new[] { 3.0f, 5.6f })
                Prims.Box(t, "PiedEtabli", new Vector3(lx, 0.42f, 3.45f), new Vector3(0.08f, 0.84f, 0.8f), new Color(0.3f, 0.3f, 0.32f));
            Prims.Box(t, "CaisseOutils", new Vector3(3.7f, 1.05f, 3.4f), new Vector3(0.6f, 0.3f, 0.3f), new Color(0.8f, 0.12f, 0.1f));
            Prims.Box(t, "Etau", new Vector3(5.1f, 0.98f, 3.25f), new Vector3(0.25f, 0.16f, 0.18f), new Color(0.2f, 0.35f, 0.6f));
            Prims.Box(t, "Panneau", new Vector3(4.3f, 1.8f, 3.98f), new Vector3(2.6f, 1.1f, 0.04f), new Color(0.55f, 0.45f, 0.32f));
            for (int i = 0; i < 6; i++)
                Prims.Box(t, "Cle", new Vector3(3.3f + i * 0.38f, 1.85f, 3.94f), new Vector3(0.05f, 0.5f, 0.02f), new Color(0.75f, 0.76f, 0.78f));

            // Pile de pneus, fûts d'huile, bidons
            for (int i = 0; i < 5; i++)
                Prims.Cyl(t, "Pneu", new Vector3(-4.6f, 0.11f + i * 0.21f, 3.1f), 0.66f, 0.2f, new Color(0.07f, 0.07f, 0.08f));
            for (int i = 0; i < 3; i++)
                Prims.Cyl(t, "Pneu", new Vector3(-3.85f, 0.11f + i * 0.21f, 3.35f), 0.62f, 0.2f, new Color(0.08f, 0.08f, 0.09f));
            Prims.Cyl(t, "Fut", new Vector3(-5.0f, 0.44f, 1.5f), 0.58f, 0.88f, new Color(0.1f, 0.3f, 0.7f));
            Prims.Cyl(t, "Fut", new Vector3(-4.35f, 0.44f, 1.85f), 0.58f, 0.88f, new Color(0.75f, 0.12f, 0.1f));
            Prims.Box(t, "Bidon", new Vector3(-4.9f, 0.23f, 0.6f), new Vector3(0.18f, 0.46f, 0.36f), new Color(0.95f, 0.78f, 0.1f));
            Prims.Box(t, "Bidon", new Vector3(-4.62f, 0.23f, 0.75f), new Vector3(0.18f, 0.46f, 0.36f), new Color(0.95f, 0.78f, 0.1f));

            // Néons et lumières chaudes
            Material neon = Mats.Unlit(new Color(1f, 0.98f, 0.9f));
            foreach (float lx in new[] { -2.2f, 2.4f })
            {
                Prims.Box(t, "Neon", new Vector3(lx, 4.05f, 1.2f), new Vector3(1.5f, 0.06f, 0.12f), Color.white).GetComponent<Renderer>().sharedMaterial = neon;
                GameObject lg = new GameObject("Lumiere");
                lg.transform.SetParent(t, false);
                lg.transform.localPosition = new Vector3(lx, 3.7f, 1.0f);
                Light l = lg.AddComponent<Light>();
                l.type = LightType.Point;
                l.range = 9f;
                l.intensity = 1.6f;
                l.color = new Color(1f, 0.92f, 0.78f);
                l.shadows = LightShadows.None;
            }

            motoSlot = new GameObject("PlaceMoto").transform;
            motoSlot.SetParent(t, false);
            motoSlot.localPosition = new Vector3(0.65f, 0f, 0.9f);
            motoSlot.localRotation = Quaternion.Euler(0f, -32f, 0f);
            riderSlot = new GameObject("PlaceChauffeur").transform;
            riderSlot.SetParent(t, false);
            riderSlot.localPosition = new Vector3(1.75f, 0f, 0.35f);
            riderSlot.localRotation = Quaternion.Euler(0f, 205f, 0f);

            // Plateau tournant (tôle striée) : le véhicule choisi y tourne dans le garage du menu
            turntable = new GameObject("Plateau").transform;
            turntable.SetParent(t, false);
            turntable.localPosition = new Vector3(0.45f, 0f, 1.0f);
            Material plate = TexMat("tole", new Color(0.32f, 0.33f, 0.36f), new Vector2(6f, 6f), 0.55f);
            GameObject disc = Prims.Cyl(turntable, "Disque", new Vector3(0f, 0.03f, 0f), 3.4f, 0.06f, Color.gray);
            disc.GetComponent<Renderer>().sharedMaterial = plate;
            Prims.Cyl(turntable, "Bord", new Vector3(0f, 0.015f, 0f), 3.5f, 0.03f, new Color(0.12f, 0.12f, 0.13f));
            tableSlot = new GameObject("PlaceSurPlateau").transform;
            tableSlot.SetParent(turntable, false);
            tableSlot.localPosition = new Vector3(0f, 0.06f, 0f);
            turntable.gameObject.SetActive(false);
        }

        /// <summary>Garage du menu : plateau tournant visible (vrai) ou moto exposée devant le rideau (faux).</summary>
        public void SetShowroom(bool on)
        {
            showroom = on;
            if (turntable != null) turntable.gameObject.SetActive(on);
            if (moto != null) moto.SetActive(!on);
            if (!on && shown != null) { Destroy(shown); shown = null; shownKey = null; }
            // Le chauffeur reste debout à côté du plateau
            if (riderSlot != null) riderSlot.localPosition = on ? new Vector3(2.05f, 0f, 0.15f) : new Vector3(1.75f, 0f, 0.35f);
        }

        /// <summary>Véhicule posé sur le plateau tournant (possédé ou modèle du magasin).</summary>
        public void ShowVehicle(VehicleDef def, OwnedVehicle ov)
        {
            if (def == null || tableSlot == null) return;
            string key = def.id + "|" + (ov != null ? ov.instanceId + ov.couleurPrincipale + ov.couleurSecondaire + ov.jantes + ov.autocollant + ov.plaque : "neuf");
            if (key == shownKey && shown != null) return;
            if (shown != null) Destroy(shown);
            shown = VehicleBuilder.BuildPreview(def, ov);
            shown.transform.SetParent(tableSlot, false);
            shown.transform.localPosition = new Vector3(0f, 0f, def.IsMoto ? 0f : -0.1f);
            shown.transform.localRotation = Quaternion.identity;
            foreach (Collider c in shown.GetComponentsInChildren<Collider>()) c.enabled = false;
            foreach (Rigidbody r in shown.GetComponentsInChildren<Rigidbody>()) r.isKinematic = true;
            shownKey = key;
        }

        /// <summary>Change seulement le chauffeur debout (onglet CHAUFFEUR du garage).</summary>
        public void RefreshRider()
        {
            string av = GameSettings.DriverAvatar ?? "";
            if (av == riderKey && rider != null) return;
            if (rider != null) Destroy(rider);
            rider = HumanLibrary.SpawnNamed(av, riderSlot, Vector3.zero, "chauffeur", false, 5, false);
            if (rider == null) rider = VehicleBuilder.BuildHuman(riderSlot, Vector3.zero, "chauffeur", false, 5, false);
            rider.transform.localRotation = Quaternion.identity;
            foreach (Animator a in rider.GetComponentsInChildren<Animator>()) a.updateMode = AnimatorUpdateMode.UnscaledTime;
            riderKey = av;
        }

        private void Update()
        {
            // Le plateau tourne en temps réel (le menu met le jeu en pause)
            if (showroom && turntable != null) turntable.Rotate(0f, Time.unscaledDeltaTime * 18f, 0f, Space.Self);
        }

        /// <summary>Met à jour la moto exposée (véhicule actif de la dernière partie) et le chauffeur choisi.</summary>
        public void Show(GameDatabase db, PlayerProfile profile)
        {
            if (db == null) return;
            VehicleDef def = null;
            OwnedVehicle ov = null;
            if (profile != null)
            {
                ov = profile.FindVehicle(profile.vehiculeActif);
                if (ov != null) def = db.Vehicle(ov.defId);
            }
            if (def == null)
                foreach (VehicleDef d in db.Vehicles) if (d.IsMoto) { def = d; break; }
            if (def == null && db.Vehicles.Count > 0) def = db.Vehicles[0];
            if (def != null)
            {
                string key = def.id + "|" + (ov != null ? ov.instanceId + ov.couleurPrincipale + ov.couleurSecondaire + ov.jantes + ov.autocollant + ov.plaque : "");
                if (key != motoKey || moto == null)
                {
                    if (moto != null) Destroy(moto);
                    moto = VehicleBuilder.BuildPreview(def, ov);
                    moto.transform.SetParent(motoSlot, false);
                    moto.transform.localPosition = Vector3.zero;
                    moto.transform.localRotation = Quaternion.identity;
                    foreach (Collider c in moto.GetComponentsInChildren<Collider>()) c.enabled = false;
                    foreach (Rigidbody r in moto.GetComponentsInChildren<Rigidbody>()) r.isKinematic = true;
                    motoKey = key;
                    // Tricycle plus long : on le recule un peu pour qu'il tienne dans le cadre
                    motoSlot.localPosition = def.IsMoto ? new Vector3(0.65f, 0f, 0.9f) : new Vector3(0.2f, 0f, 1.6f);
                }
                if (moto != null) moto.SetActive(!showroom);
            }
            string av = GameSettings.DriverAvatar ?? "";
            if (av != riderKey || rider == null)
            {
                if (rider != null) Destroy(rider);
                rider = HumanLibrary.SpawnNamed(av, riderSlot, Vector3.zero, "chauffeur", false, 5, false);
                if (rider == null) rider = VehicleBuilder.BuildHuman(riderSlot, Vector3.zero, "chauffeur", false, 5, false);
                rider.transform.localRotation = Quaternion.identity;
                // Le menu met le jeu en pause : le chauffeur respire quand même (animation en temps réel)
                foreach (Animator a in rider.GetComponentsInChildren<Animator>()) a.updateMode = AnimatorUpdateMode.UnscaledTime;
                riderKey = av;
            }
        }
    }
}
