// BALULA MOTO — motos construites à partir des modèles envoyés par MATULA DESIGN (scripts/convertir_modeles_recus.py) :
//   MotoCentrale   (moto1 / motor.blend)            : moto centrale du jeu (moto de départ)
//   HondaCB750     (honda_cb_750_four_TS_00.FBX)     : à acheter, dans la circulation et les parkings
//   HondaClassique (honda.fbx, modèle exact à vérifier) : à acheter, dans la circulation et les parkings
// Groupes lus dans les OBJ : Caisse_* (fixe), Direction_* (tourne avec le guidon, autour de l'axe de direction incliné),
// RoueAV_* / RoueAR_* (roues), rôles Phare, FeuAR, ClignoG/D, Plaque, Peinture (couleur du joueur). Designs commerciaux
// respectés, sans logo et sans prétendre à un partenariat officiel avec Honda.
using System.Collections.Generic;
using BalulaMoto.Core;
using UnityEngine;

namespace BalulaMoto.Runtime
{
    public static partial class VehicleBuilder
    {
        /// <summary>Repères mesurés sur chaque modèle converti (mètres ; x vers la droite, z vers l'avant, sol à y = 0,
        /// empattement centré sur z = 0).</summary>
        public class MotoSpec
        {
            public string Model;
            public float Wheelbase, Radius, Rake;
            public Vector3 Head, Grip, Seat, Peg, Plate, Lamp, Pillion;
        }

        public static readonly MotoSpec[] MotoSpecs =
        {
            new MotoSpec { Model = "MotoCentrale", Wheelbase = 1.343f, Radius = 0.315f, Rake = 26f, Head = new Vector3(0f, 0.99f, 0.43f),
                Grip = new Vector3(0.29f, 0.99f, 0.36f), Seat = new Vector3(0f, 0.86f, -0.12f), Peg = new Vector3(0.17f, 0.34f, 0.02f),
                Plate = new Vector3(0f, 0.538f, -0.99f), Lamp = new Vector3(0f, 0.846f, 0.5f), Pillion = new Vector3(0f, 0.9f, -0.5f) },
            new MotoSpec { Model = "HondaCB750", Wheelbase = 1.413f, Radius = 0.336f, Rake = 27f, Head = new Vector3(0f, 1.0f, 0.435f),
                Grip = new Vector3(0.4f, 1.085f, 0.3f), Seat = new Vector3(0f, 0.87f, -0.15f), Peg = new Vector3(0.2f, 0.34f, -0.06f),
                Plate = new Vector3(0f, 0.5f, -1.08f), Lamp = new Vector3(0f, 0.936f, 0.62f), Pillion = new Vector3(0f, 0.9f, -0.55f) },
            new MotoSpec { Model = "HondaClassique", Wheelbase = 1.298f, Radius = 0.305f, Rake = 26f, Head = new Vector3(0f, 0.94f, 0.49f),
                Grip = new Vector3(0.3f, 0.926f, 0.317f), Seat = new Vector3(0f, 0.74f, -0.2f), Peg = new Vector3(0.17f, 0.3f, -0.02f),
                Plate = new Vector3(0f, 0.56f, -0.99f), Lamp = new Vector3(0f, 0.76f, 0.6f), Pillion = new Vector3(0f, 0.78f, -0.55f) },
        };

        public static MotoSpec FindMotoSpec(string model)
        {
            if (string.IsNullOrEmpty(model)) return null;
            foreach (MotoSpec s in MotoSpecs) if (s.Model == model) return s;
            return null;
        }

        private static readonly Dictionary<string, GameObject> motoModels = new Dictionary<string, GameObject>();

        private static GameObject MotoModel(string name)
        {
            GameObject g;
            if (motoModels.TryGetValue(name, out g)) return g;
            g = Resources.Load<GameObject>("BalulaMoto/Vehicules/" + name);
            if (g == null) Debug.LogWarning("[BalulaMoto] Modèle de moto absent : " + name);
            motoModels[name] = g;
            return g;
        }

        private struct MotoPart
        {
            public Mesh Mesh;
            public Material Src;
            public Matrix4x4 M;
            public string Group, Role, Mat;
        }

        private static List<MotoPart> MotoParts(GameObject model)
        {
            List<MotoPart> list = new List<MotoPart>();
            Matrix4x4 toModel = model.transform.worldToLocalMatrix;
            foreach (MeshFilter mf in model.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                MeshRenderer mr = mf.GetComponent<MeshRenderer>();
                string n = mf.name;
                int a = n.IndexOf('_'), b = a >= 0 ? n.IndexOf('_', a + 1) : -1;
                MotoPart p = new MotoPart
                {
                    Mesh = mf.sharedMesh,
                    Src = mr != null ? mr.sharedMaterial : null,
                    M = toModel * mf.transform.localToWorldMatrix,
                    Group = a > 0 ? n.Substring(0, a) : n,
                    Role = b > a ? n.Substring(a + 1, b - a - 1) : "Corps",
                    Mat = b > 0 ? n.Substring(b + 1) : ""
                };
                list.Add(p);
            }
            return list;
        }

        private static Vector3 GroupCenter(List<MotoPart> parts, string group)
        {
            bool any = false;
            Bounds bb = new Bounds();
            foreach (MotoPart p in parts)
            {
                if (p.Group != group) continue;
                Bounds b = DecorModels.TransformBounds(p.Mesh.bounds, p.M);
                if (!any) { bb = b; any = true; } else bb.Encapsulate(b);
            }
            return any ? bb.center : Vector3.zero;
        }

        /// <summary>Matériau d'une pièce de moto selon son rôle et la couleur du fichier.</summary>
        private static Material MotoMaterial(MotoPart p, Color paint, Color rim, bool wheel)
        {
            Color c = p.Src != null && p.Src.HasProperty("_Color") ? p.Src.color : Color.gray;
            c.a = 1f;
            if (p.Role == "Peinture") return Paint(paint, 0.3f, 0.82f);
            string m = p.Mat.ToLowerInvariant();
            float lum = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
            bool rubber = m.Contains("tire") || m.Contains("pneu") || m.Contains("gomma") || m.Contains("ban") || m.Contains("karet") || m.Contains("leather") || m.Contains("jok");
            if (rubber || lum < 0.08f) return Paint(c, 0f, 0.25f);
            if (wheel && lum > 0.5f) return Paint(rim, 0.75f, 0.8f);
            bool metal = m.Contains("chrom") || m.Contains("silver") || m.Contains("crom") || m.Contains("steel") || m.Contains("cerchi") || m.Contains("raggi") ||
                         m.Contains("besi") || m.Contains("metal") || m.Contains("tabung") || m.Contains("manubrio") || m.Contains("specchio");
            if (metal && lum > 0.5f) return Paint(c, 0.85f, 0.85f);
            return Paint(c, 0.2f, 0.5f);
        }

        private static GameObject MotoMeshPart(Transform parent, string name, MotoPart p, Material mat, Matrix4x4 local)
        {
            Material[] mats = new Material[Mathf.Max(1, p.Mesh.subMeshCount)];
            for (int i = 0; i < mats.Length; i++) mats[i] = mat;
            GameObject g = MeshPart(parent, name, p.Mesh, mats, Vector3.zero);
            ApplyMatrix(g.transform, local);
            return g;
        }

        // ==================================================================
        // Moto du joueur (garage, magasin, partie)
        // ==================================================================
        private static bool BuildModelMoto(VehicleRig rig, VehicleDef d, OwnedVehicle ov, Color main, Color second, Color rim, bool withDriver)
        {
            MotoSpec sp = FindMotoSpec(d.modele3d);
            GameObject model = sp != null ? MotoModel(sp.Model) : null;
            if (model == null) return false;
            List<MotoPart> parts = MotoParts(model);
            if (parts.Count == 0) return false;
            Transform b = rig.Body;
            float r = d.rayonRoueM > 0f ? d.rayonRoueM : sp.Radius, wb = d.empattementM > 0f ? d.empattementM : sp.Wheelbase;
            // Roues aux emplacements de la physique ; maillages centrés sur leur moyeu
            Transform fw = Anchor(b, "RoueAV", new Vector3(0f, r, wb * 0.5f));
            Transform rw = Anchor(b, "RoueAR", new Vector3(0f, r, -wb * 0.5f));
            rig.Wheels.Add(fw);
            rig.Wheels.Add(rw);
            Vector3 fc = GroupCenter(parts, "RoueAV"), rc = GroupCenter(parts, "RoueAR");
            // Direction : axe incliné (chasse), le guidon tourne autour de lui
            Transform axis = Anchor(b, "AxeDirection", sp.Head);
            axis.localRotation = Quaternion.Euler(-sp.Rake, 0f, 0f);
            Transform pivot = Anchor(axis, "Direction", Vector3.zero);
            rig.SteerPivot = pivot;
            Matrix4x4 toPivot = (Matrix4x4.TRS(sp.Head, axis.localRotation, Vector3.one)).inverse;
            Vector3 plate = sp.Plate;
            foreach (MotoPart p in parts)
            {
                bool wheel = p.Group == "RoueAV" || p.Group == "RoueAR";
                Material mat = MotoMaterial(p, main, rim, wheel);
                if (p.Group == "RoueAV") { MotoMeshPart(fw, "Roue", p, mat, Matrix4x4.Translate(-fc) * p.M); continue; }
                if (p.Group == "RoueAR") { MotoMeshPart(rw, "Roue", p, mat, Matrix4x4.Translate(-rc) * p.M); continue; }
                bool steer = p.Group == "Direction";
                Transform parent = steer ? pivot : b;
                Matrix4x4 local = steer ? toPivot * p.M : p.M;
                switch (p.Role)
                {
                    case "Phare":
                        rig.HeadLights.Add(MotoMeshPart(parent, "Phare", p, Mats.Lit(new Color(0.6f, 0.6f, 0.55f), 0.8f), local).GetComponent<Renderer>());
                        break;
                    case "FeuAR":
                        rig.TailLights.Add(MotoMeshPart(parent, "FeuArriere", p, Mats.Lit(RedLight, 0.5f), local).GetComponent<Renderer>());
                        break;
                    case "ClignoG":
                        rig.IndicatorsLeft.Add(MotoMeshPart(parent, "Clignotant", p, Mats.Lit(ColorUtil.Shade(Amber, 0.3f), 0.5f), local).GetComponent<Renderer>());
                        break;
                    case "ClignoD":
                        rig.IndicatorsRight.Add(MotoMeshPart(parent, "Clignotant", p, Mats.Lit(ColorUtil.Shade(Amber, 0.3f), 0.5f), local).GetComponent<Renderer>());
                        break;
                    case "Plaque":
                        MotoMeshPart(parent, "PlaqueModele", p, mat, local);
                        plate = DecorModels.TransformBounds(p.Mesh.bounds, p.M).center;
                        break;
                    default:
                        MotoMeshPart(parent, p.Role == "Peinture" ? "Peinture" : "Piece", p, mat, local);
                        break;
                }
            }
            // Feux : si le modèle n'a pas de clignotants arrière ou de feu, petits feux du jeu
            if (rig.TailLights.Count == 0)
                rig.TailLights.Add(Lamp(b, "FeuArriere", new Vector3(0f, sp.Plate.y + 0.22f, sp.Plate.z + 0.06f), new Vector3(0.12f, 0.06f, 0.04f), RedLight));
            rig.HeadLamp = HeadLamp(pivot, toPivot.MultiplyPoint3x4(sp.Lamp));
            rig.PlateAnchor = Anchor(b, "PlaqueJoueur", plate + new Vector3(0f, 0f, -0.02f));
            rig.StickerAnchor = Anchor(b, "Autocollant", new Vector3(0.17f, sp.Seat.y - 0.12f, sp.Seat.z - 0.1f));
            rig.CameraFirstPerson = Anchor(b, "CameraPremierePersonne", new Vector3(0f, sp.Seat.y + 0.72f, sp.Seat.z + 0.04f));
            rig.CameraDashboard = Anchor(b, "CameraTableauDeBord", new Vector3(0f, sp.Seat.y + 0.55f, sp.Seat.z + 0.18f));
            rig.Seats.Add(Anchor(b, "PlacePassager1", sp.Pillion));
            if (d.passagers >= 2) rig.Seats.Add(Anchor(b, "PlacePassager2", sp.Pillion + new Vector3(0f, 0.02f, -0.22f)));
            rig.CargoAnchor = Anchor(b, "Bagages", new Vector3(0f, sp.Pillion.y + 0.06f, sp.Pillion.z - 0.2f));
            if (withDriver)
            {
                rig.Driver = BuildDriver(b, sp.Seat, 7, true);
                SetArmsForward(rig.Driver, 52f);
                Vector3 gl = new Vector3(-sp.Grip.x + 0.04f, sp.Grip.y, sp.Grip.z), gr = new Vector3(sp.Grip.x - 0.04f, sp.Grip.y, sp.Grip.z);
                HumanLibrary.SetupRider(rig.Driver, Anchor(pivot, "MainGauche", toPivot.MultiplyPoint3x4(gl)), Anchor(pivot, "MainDroite", toPivot.MultiplyPoint3x4(gr)),
                    Anchor(b, "PiedGauche", new Vector3(-sp.Peg.x, sp.Peg.y, sp.Peg.z)), Anchor(b, "PiedDroit", new Vector3(sp.Peg.x, sp.Peg.y, sp.Peg.z)), 10f);
            }
            return true;
        }

        // ==================================================================
        // Motos de la circulation (version légère du modèle, avec son pilote)
        // ==================================================================
        private static GameObject BuildModelMotoTraffic(GameObject root, string modelName, System.Random rnd, int seed, out float length, List<Renderer> brakeLights, List<Renderer> hazards, List<Transform> wheels)
        {
            length = 2.1f;
            MotoSpec sp = FindMotoSpec(modelName);
            GameObject model = sp != null ? MotoModel(sp.Model + "_LOD") : null;
            if (model == null) return null;
            List<MotoPart> parts = MotoParts(model);
            if (parts.Count == 0) return null;
            Transform t = root.transform;
            root.name = "PNJ_MotoTaxi_" + modelName;
            Color paint = ColorUtil.Hex(Prints[rnd.Next(Prints.Length)]);
            if (rnd.Next(3) == 0) paint = new Color(0.62f, 0.03f, 0.03f);
            Vector3 fc = GroupCenter(parts, "RoueAV"), rc = GroupCenter(parts, "RoueAR");
            Transform fw = Anchor(t, "Roue", fc);
            Transform rw = Anchor(t, "Roue", rc);
            wheels.Add(fw);
            wheels.Add(rw);
            foreach (MotoPart p in parts)
            {
                bool wheel = p.Group == "RoueAV" || p.Group == "RoueAR";
                Material mat = MotoMaterial(p, paint, Silver, wheel);
                if (wheel)
                {
                    Color c = p.Src != null && p.Src.HasProperty("_Color") ? p.Src.color : Color.gray;
                    bool rimPart = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f > 0.3f;
                    Transform w = p.Group == "RoueAV" ? fw : rw;
                    MotoMeshPart(w, rimPart ? "Jante" : "Pneu", p, mat, Matrix4x4.Translate(-(p.Group == "RoueAV" ? fc : rc)) * p.M);
                    continue;
                }
                switch (p.Role)
                {
                    case "Phare": MotoMeshPart(t, "Phare", p, Mats.Lit(new Color(0.85f, 0.85f, 0.8f), 0.8f), p.M); break;
                    case "FeuAR": brakeLights.Add(MotoMeshPart(t, "Stop", p, Mats.Lit(RedLight, 0.5f), p.M).GetComponent<Renderer>()); break;
                    case "ClignoG":
                    case "ClignoD": hazards.Add(MotoMeshPart(t, "Warning", p, Mats.Lit(ColorUtil.Shade(Amber, 0.3f), 0.5f), p.M).GetComponent<Renderer>()); break;
                    default: MotoMeshPart(t, "Piece", p, mat, p.M); break;
                }
            }
            if (brakeLights.Count == 0) brakeLights.Add(Lamp(t, "Stop", new Vector3(0f, sp.Plate.y + 0.22f, sp.Plate.z + 0.05f), new Vector3(0.12f, 0.06f, 0.04f), RedLight));
            GameObject rider = BuildHuman(t, sp.Seat + new Vector3(0f, 0.04f, 0f), "chauffeur", true, seed, rnd.Next(3) == 0);
            HumanLibrary.SetupRider(rider, Anchor(t, "MainGauche", new Vector3(-sp.Grip.x + 0.04f, sp.Grip.y, sp.Grip.z)), Anchor(t, "MainDroite", new Vector3(sp.Grip.x - 0.04f, sp.Grip.y, sp.Grip.z)),
                Anchor(t, "PiedGauche", new Vector3(-sp.Peg.x, sp.Peg.y + 0.04f, sp.Peg.z)), Anchor(t, "PiedDroit", new Vector3(sp.Peg.x, sp.Peg.y + 0.04f, sp.Peg.z)), 10f);
            HumanLibrary.MakeStatic(rider, false);
            if (rnd.Next(2) == 0)
            {
                GameObject pass = BuildHuman(t, sp.Pillion + new Vector3(0f, 0.02f, 0f), rnd.Next(2) == 0 ? "pagne" : "civil", true, seed + 3, false);
                HumanLibrary.SetupRider(pass, null, null, Anchor(t, "PiedPassagerG", new Vector3(-0.2f, sp.Peg.y + 0.08f, sp.Pillion.z + 0.1f)), Anchor(t, "PiedPassagerD", new Vector3(0.2f, sp.Peg.y + 0.08f, sp.Pillion.z + 0.1f)), 0f);
                HumanLibrary.MakeStatic(pass, false);
            }
            length = sp.Wheelbase + 0.75f;
            AddTrafficBody(root, new Vector3(0.6f, 1.6f, length), new Vector3(0f, 0.8f, 0f));
            return root;
        }
    }
}
