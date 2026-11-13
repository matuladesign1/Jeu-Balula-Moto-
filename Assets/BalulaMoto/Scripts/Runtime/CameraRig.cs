// BALULA MOTO — caméras : 3e personne, rapprochée, 1re personne, tableau de bord (tricycle), libre.
using BalulaMoto.Core;
using UnityEngine;

namespace BalulaMoto.Runtime
{
    public enum CameraMode { TroisiemePersonne, Rapprochee, PremierePersonne, TableauDeBord, Libre }

    public class CameraRig : MonoBehaviour
    {
        public Camera Cam;
        public RaycastVehicle Target;
        public CameraMode Mode = CameraMode.TroisiemePersonne;
        public float Sensitivity = 1f;

        private float orbitYaw, orbitPitch = 18f, orbitDistance = 9f;
        private Vector3 velocity;
        private bool menuOrbit, menuShot;
        private Vector3 menuCenter, shotPos, shotLook;
        private float menuRadius = 60f;
        private float menuAngle;
        private float chaseYaw, armLength = -1f;
        private bool chaseInit;
        private static readonly RaycastHit[] hitBuffer = new RaycastHit[16];

        public static string Label(CameraMode m)
        {
            switch (m)
            {
                case CameraMode.TroisiemePersonne: return "Caméra arrière";
                case CameraMode.Rapprochee: return "Caméra rapprochée";
                case CameraMode.PremierePersonne: return "1re personne";
                case CameraMode.TableauDeBord: return "Tableau de bord";
                case CameraMode.Libre: return "Caméra libre";
            }
            return "";
        }

        public static CameraRig Create()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                GameObject go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            // Plan proche un peu plus loin : meilleure précision de profondeur (routes et sol ne se mélangent plus au loin)
            cam.nearClipPlane = 0.12f;
            cam.farClipPlane = 1800f;
            cam.fieldOfView = 62f;
            CameraRig rig = cam.gameObject.AddComponent<CameraRig>();
            rig.Cam = cam;
            return rig;
        }

        public void SetTarget(RaycastVehicle v)
        {
            Target = v;
            menuOrbit = false;
            menuShot = false;
            chaseInit = false;
            armLength = -1f;
            if (v != null)
            {
                orbitYaw = v.transform.eulerAngles.y;
                if (Mode == CameraMode.TableauDeBord && v.Kind == VehicleKind.Moto) Mode = CameraMode.TroisiemePersonne;
                SnapBehind();
            }
        }

        public void SetMenuOrbit(Vector3 center, float radius)
        {
            menuOrbit = true;
            menuShot = false;
            menuCenter = center;
            menuRadius = radius;
        }

        /// <summary>Plan fixe du menu (garage) : la caméra respire légèrement autour de sa position.</summary>
        public void SetMenuShot(Vector3 position, Vector3 lookAt)
        {
            menuShot = true;
            menuOrbit = true;
            shotPos = position;
            shotLook = lookAt;
            transform.position = position;
            transform.LookAt(lookAt);
        }

        public CameraMode NextMode()
        {
            int m = ((int)Mode + 1) % 5;
            if ((CameraMode)m == CameraMode.TableauDeBord && Target != null && Target.Kind == VehicleKind.Moto) m = (m + 1) % 5;
            Mode = (CameraMode)m;
            if (Mode == CameraMode.Libre && Target != null) orbitYaw = Target.transform.eulerAngles.y;
            return Mode;
        }

        /// <summary>Glisser le doigt sur l'écran : seulement en caméra libre. Les caméras de conduite restent
        /// fixes derrière le véhicule (demande du créateur : caméra fixe qui suit la moto).</summary>
        public void AddOrbit(Vector2 delta)
        {
            if (Mode != CameraMode.Libre) return;
            float k = 0.18f * Mathf.Max(0.3f, Sensitivity);
            orbitYaw += delta.x * k;
            orbitPitch = Mathf.Clamp(orbitPitch - delta.y * k, -5f, 75f);
        }

        public void Zoom(float amount) { if (Mode == CameraMode.Libre) orbitDistance = Mathf.Clamp(orbitDistance - amount, 3f, 40f); }

        private void SnapBehind()
        {
            if (Target == null) return;
            Vector3 pos, look;
            Compute(out pos, out look, true, 0f);
            transform.position = pos;
            transform.LookAt(look);
        }

        private void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            if (menuOrbit || Target == null)
            {
                if (menuShot)
                {
                    float tt = Time.unscaledTime;
                    Vector3 sway = new Vector3(Mathf.Sin(tt * 0.21f) * 0.35f, Mathf.Sin(tt * 0.33f) * 0.12f, 0f);
                    transform.position = Vector3.Lerp(transform.position, shotPos + sway, dt * 2f);
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(shotLook - transform.position), dt * 4f);
                    Cam.fieldOfView = Mathf.Lerp(Cam.fieldOfView, 50f, dt * 3f);
                    return;
                }
                menuAngle += dt * 4f;
                Vector3 p = menuCenter + Quaternion.Euler(0f, menuAngle, 0f) * new Vector3(0f, menuRadius * 0.45f, -menuRadius);
                transform.position = Vector3.Lerp(transform.position, p, dt * 2f);
                transform.LookAt(menuCenter + Vector3.up * 5f);
                return;
            }
            Vector3 targetPos, lookAt;
            Compute(out targetPos, out lookAt, false, dt);
            bool inside = Mode == CameraMode.PremierePersonne || Mode == CameraMode.TableauDeBord;
            if (inside)
            {
                transform.position = targetPos;
                Transform anchor = Mode == CameraMode.PremierePersonne ? Target.Rig.CameraFirstPerson : Target.Rig.CameraDashboard;
                Quaternion baseRot = anchor != null ? anchor.rotation : Target.transform.rotation;
                transform.rotation = baseRot * Quaternion.Euler(Mode == CameraMode.PremierePersonne ? 9f : 4f, 0f, 0f);
            }
            else
            {
                // Pas de passage à travers les murs : la caméra se rapproche quand un mur ou une maison la gêne
                Vector3 safe = AvoidWalls(lookAt, targetPos, dt);
                if (Mode == CameraMode.Libre) transform.position = Vector3.SmoothDamp(transform.position, safe, ref velocity, 0.25f, Mathf.Infinity, dt);
                else transform.position = safe;
                transform.LookAt(lookAt);
            }
            Cam.fieldOfView = Mathf.Lerp(Cam.fieldOfView, inside ? 72f : 60f + Mathf.Clamp(Target.SpeedKmh / 12f, 0f, 8f), dt * 3f);
        }

        private Vector3 AvoidWalls(Vector3 pivot, Vector3 desired, float dt)
        {
            Vector3 d = desired - pivot;
            float len = d.magnitude;
            if (len < 0.05f) return desired;
            Vector3 dir = d / len;
            int mask = ~(1 << RaycastVehicle.PlayerLayer);
            int n = Physics.SphereCastNonAlloc(pivot, 0.3f, dir, hitBuffer, len, mask, QueryTriggerInteraction.Ignore);
            float best = len;
            for (int i = 0; i < n; i++)
            {
                RaycastHit h = hitBuffer[i];
                if (h.collider == null || h.collider.attachedRigidbody != null || h.distance <= 0f) continue;
                if (Target != null && h.collider.transform.IsChildOf(Target.transform)) continue;
                best = Mathf.Min(best, Mathf.Max(0.8f, h.distance - 0.15f));
            }
            if (armLength < 0f || best < armLength) armLength = best;
            else armLength = Mathf.MoveTowards(armLength, best, dt * 5f);
            return pivot + dir * armLength;
        }

        private void Compute(out Vector3 pos, out Vector3 look, bool snap, float dt)
        {
            Transform t = Target.transform;
            bool moto = Target.Kind == VehicleKind.Moto;
            Vector3 flatFwd = Vector3.ProjectOnPlane(t.forward, Vector3.up);
            if (flatFwd.sqrMagnitude < 0.01f) flatFwd = Vector3.forward;
            flatFwd.Normalize();
            switch (Mode)
            {
                case CameraMode.PremierePersonne:
                    // Un peu en avant des yeux : on voit le guidon, les rétroviseurs et la route, pas l'intérieur du casque
                    pos = (Target.Rig.CameraFirstPerson != null ? Target.Rig.CameraFirstPerson.position : t.position + Vector3.up * 1.6f) + t.forward * 0.14f;
                    look = pos + t.forward;
                    return;
                case CameraMode.TableauDeBord:
                    pos = Target.Rig.CameraDashboard != null ? Target.Rig.CameraDashboard.position : t.position + Vector3.up * 1.4f;
                    look = pos + t.forward;
                    return;
                case CameraMode.Libre:
                    {
                        Quaternion q = Quaternion.Euler(orbitPitch, orbitYaw, 0f);
                        look = t.position + Vector3.up * 1.2f;
                        pos = look - q * Vector3.forward * orbitDistance;
                        if (pos.y < 0.5f) pos.y = 0.5f;
                        return;
                    }
                default:
                    {
                        // Caméra de poursuite fixe : toujours derrière le véhicule, le cap est seulement adouci
                        bool close = Mode == CameraMode.Rapprochee;
                        float dist = close ? (moto ? 3.4f : 4.8f) : (moto ? 5.4f : 7.4f);
                        float height = close ? (moto ? 1.75f : 2.4f) : (moto ? 2.25f : 3.0f);
                        float vehYaw = Quaternion.LookRotation(flatFwd).eulerAngles.y;
                        if (snap || !chaseInit) { chaseYaw = vehYaw; chaseInit = true; }
                        else chaseYaw = Mathf.LerpAngle(chaseYaw, vehYaw, 1f - Mathf.Exp(-dt * 8f));
                        orbitYaw = chaseYaw;
                        Vector3 dir = Quaternion.Euler(0f, chaseYaw, 0f) * Vector3.forward;
                        pos = t.position - dir * dist + Vector3.up * height;
                        look = t.position + Vector3.up * (moto ? 1.15f : 1.45f) + dir * 2.2f;
                        return;
                    }
            }
        }
    }
}
