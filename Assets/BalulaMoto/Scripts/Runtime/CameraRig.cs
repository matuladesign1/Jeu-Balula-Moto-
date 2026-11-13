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
        private bool menuOrbit;
        private Vector3 menuCenter;
        private float menuRadius = 60f;
        private float menuAngle;

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
            cam.nearClipPlane = 0.08f;
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
            menuCenter = center;
            menuRadius = radius;
        }

        public CameraMode NextMode()
        {
            int m = ((int)Mode + 1) % 5;
            if ((CameraMode)m == CameraMode.TableauDeBord && Target != null && Target.Kind == VehicleKind.Moto) m = (m + 1) % 5;
            Mode = (CameraMode)m;
            if (Mode == CameraMode.Libre && Target != null) orbitYaw = Target.transform.eulerAngles.y;
            return Mode;
        }

        /// <summary>Glisser le doigt sur l'écran : orbite en caméra libre, regard en 3e personne.</summary>
        public void AddOrbit(Vector2 delta)
        {
            float k = 0.18f * Mathf.Max(0.3f, Sensitivity);
            orbitYaw += delta.x * k;
            orbitPitch = Mathf.Clamp(orbitPitch - delta.y * k, -5f, 75f);
            if (Mode != CameraMode.Libre && Mode != CameraMode.TroisiemePersonne && Mode != CameraMode.Rapprochee) return;
            lookOffsetTimer = 2.5f;
        }

        public void Zoom(float amount) { orbitDistance = Mathf.Clamp(orbitDistance - amount, 3f, 40f); }

        private float lookOffsetTimer;

        private void SnapBehind()
        {
            if (Target == null) return;
            Vector3 pos, look;
            Compute(out pos, out look, true);
            transform.position = pos;
            transform.LookAt(look);
        }

        private void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            if (menuOrbit || Target == null)
            {
                menuAngle += dt * 4f;
                Vector3 p = menuCenter + Quaternion.Euler(0f, menuAngle, 0f) * new Vector3(0f, menuRadius * 0.45f, -menuRadius);
                transform.position = Vector3.Lerp(transform.position, p, dt * 2f);
                transform.LookAt(menuCenter + Vector3.up * 5f);
                return;
            }
            Vector3 targetPos, lookAt;
            Compute(out targetPos, out lookAt, false);
            bool inside = Mode == CameraMode.PremierePersonne || Mode == CameraMode.TableauDeBord;
            if (inside)
            {
                transform.position = targetPos;
                Transform anchor = Mode == CameraMode.PremierePersonne ? Target.Rig.CameraFirstPerson : Target.Rig.CameraDashboard;
                Quaternion baseRot = anchor != null ? anchor.rotation : Target.transform.rotation;
                transform.rotation = baseRot * Quaternion.Euler(4f, 0f, 0f);
            }
            else
            {
                transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref velocity, Mode == CameraMode.Libre ? 0.25f : 0.12f, Mathf.Infinity, dt);
                transform.LookAt(lookAt);
            }
            Cam.fieldOfView = Mathf.Lerp(Cam.fieldOfView, inside ? 70f : 60f + Mathf.Clamp(Target.SpeedKmh / 12f, 0f, 9f), dt * 3f);
            if (lookOffsetTimer > 0f) lookOffsetTimer -= dt;
        }

        private void Compute(out Vector3 pos, out Vector3 look, bool snap)
        {
            Transform t = Target.transform;
            bool moto = Target.Kind == VehicleKind.Moto;
            Vector3 flatFwd = Vector3.ProjectOnPlane(t.forward, Vector3.up);
            if (flatFwd.sqrMagnitude < 0.01f) flatFwd = Vector3.forward;
            flatFwd.Normalize();
            switch (Mode)
            {
                case CameraMode.PremierePersonne:
                    pos = Target.Rig.CameraFirstPerson != null ? Target.Rig.CameraFirstPerson.position : t.position + Vector3.up * 1.6f;
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
                        bool close = Mode == CameraMode.Rapprochee;
                        float dist = close ? (moto ? 3.2f : 4.6f) : (moto ? 5.6f : 7.6f);
                        float height = close ? (moto ? 1.7f : 2.4f) : (moto ? 2.3f : 3.1f);
                        Vector3 dir = flatFwd;
                        if (lookOffsetTimer > 0f) dir = Quaternion.Euler(0f, orbitYaw - Quaternion.LookRotation(flatFwd).eulerAngles.y, 0f) * flatFwd;
                        else orbitYaw = Quaternion.LookRotation(flatFwd).eulerAngles.y;
                        float speedLook = Mathf.Clamp(Target.SpeedKmh / 20f, 0f, 2.5f);
                        pos = t.position - dir * dist + Vector3.up * height;
                        look = t.position + Vector3.up * (moto ? 1.1f : 1.4f) + flatFwd * speedLook;
                        return;
                    }
            }
        }
    }
}
