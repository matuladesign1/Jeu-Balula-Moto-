// BALULA MOTO — carte réelle importée d'OpenStreetMap (composant enregistré dans la scène).
// Dans son propre fichier : Unity exige que le nom du fichier corresponde à la classe pour les composants sauvegardés.
using System.Collections.Generic;
using UnityEngine;

namespace BalulaMoto.Runtime
{
    /// <summary>
    /// Carte réelle générée par l'outil d'import OpenStreetMap (menu BALULA MOTO). Quand ce composant
    /// est présent dans la scène, le jeu l'utilise à la place de la piste d'essai.
    /// </summary>
    public class ImportedWorld : MonoBehaviour
    {
        public string worldName = "Kinshasa";
        public double originLat;
        public double originLon;
        public Vector3 spawnPosition;
        public float spawnHeading;
        /// <summary>Demi-étendue de la carte importée (m), pour placer les autres zones à l'écart.</summary>
        public Vector2 halfSize = new Vector2(3000f, 3000f);
        public string attribution = "© les contributeurs d'OpenStreetMap — données sous licence ODbL";
        public List<ImportedPlace> places = new List<ImportedPlace>();
        public List<ImportedPath> trafficPaths = new List<ImportedPath>();
        /// <summary>Trottoirs (bords des rues OpenStreetMap) utilisés par la foule.</summary>
        public List<ImportedPath> walkPaths = new List<ImportedPath>();
        /// <summary>Axes des rues (parcelles clôturées générées autour du joueur, carte du jeu).</summary>
        public List<ImportedRoad> roads = new List<ImportedRoad>();
        /// <summary>Bâtiments OpenStreetMap : x, z = centre, y = rayon (les parcelles générées les évitent).</summary>
        public List<Vector3> buildingSpots = new List<Vector3>();
        /// <summary>Routes en travaux (chaussée défoncée, barrières, sable, engins) choisies à l'import.</summary>
        public List<WorkZone> works = new List<WorkZone>();
        /// <summary>Rivières, ruisseaux et canaux (tracés OpenStreetMap) : carte du jeu.</summary>
        public List<ImportedRoad> waterways = new List<ImportedRoad>();
        /// <summary>Emprise des cours d'eau en tranchée (eau + berges) : morceaux d'axe (ax, az, bx, bz) et demi-largeur
        /// au sommet des berges. Ni parcelle, ni poteau, ni maison dedans.</summary>
        public List<Vector4> waterSegs = new List<Vector4>();
        public List<float> waterHw = new List<float>();
        /// <summary>Terrains réservés (monuments clôturés) : x, z = centre, y = rayon. Ni parcelle ni poteau dedans.</summary>
        public List<Vector3> clearZones = new List<Vector3>();
        /// <summary>Espace vert du boulevard Lumumba (entre grand et petit boulevard), calculé à l'import : 4 coins par
        /// morceau (I0, I1, O1, O0) ; info = largeurs aux deux stations et voie du petit boulevard.</summary>
        public List<Vector2> greenStripPts = new List<Vector2>();
        public List<Vector3> greenStripInfo = new List<Vector3>();
        /// <summary>Arbres posés à l'import (esplanades, ronds-points) : x, z = pied, y = hauteur (m). Les modèles
        /// fournis par MATULA DESIGN y sont posés en jeu (CityFill), avec un tronc solide.</summary>
        public List<Vector3> treeSpots = new List<Vector3>();
        /// <summary>Lieux aménagés à l'import : esplanades clôturées, terrains de sport, cité clôturée et ses entrées
        /// contrôlées, écoles (voir ImportedArea).</summary>
        public List<ImportedArea> areas = new List<ImportedArea>();
        [TextArea(3, 12)] public string report;
    }

    /// <summary>Lieu aménagé : « esplanade », « cite » (contour pts), « foot », « basket », « ecole » (rectangle orienté
    /// c, x, hx, hz), « porte » (entrée contrôlée : c, x = direction de la rue, hx = demi-largeur du passage),
    /// « police » (agent en faction : c, x = regard), « fete » (place où la foule fait la fête : c, hx = rayon).</summary>
    [System.Serializable]
    public class ImportedArea
    {
        public string kind;
        public string name;
        public Vector2 c;
        public Vector2 x = new Vector2(1f, 0f);
        public float hx, hz;
        /// <summary>Vrai si l'emplacement vient d'OpenStreetMap ; faux s'il a été choisi par le jeu (à vérifier).</summary>
        public bool osm;
        public List<Vector2> pts = new List<Vector2>();

        public Vector2 Z { get { return new Vector2(-x.y, x.x); } }

        /// <summary>Vrai si le point est dans le contour (pts) ou, sans contour, dans le rectangle orienté.</summary>
        public bool Contains(Vector2 p)
        {
            if (pts != null && pts.Count >= 3)
            {
                bool inside = false;
                for (int i = 0, j = pts.Count - 1; i < pts.Count; j = i++)
                {
                    Vector2 a = pts[i], b = pts[j];
                    if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
                }
                return inside;
            }
            Vector2 d = p - c;
            return Mathf.Abs(Vector2.Dot(d, x)) <= hx && Mathf.Abs(Vector2.Dot(d, Z)) <= hz;
        }
    }

    /// <summary>Portion de route en travaux : centre, direction de la rue, longueur et largeur de chaussée.</summary>
    [System.Serializable]
    public class WorkZone
    {
        public string road;
        public Vector3 center;
        public Vector3 dir;
        public float length;
        public float width;
    }

    /// <summary>Axe d'une rue importée (portion dans la zone de la carte).</summary>
    [System.Serializable]
    public class ImportedRoad
    {
        public string name;
        public float width;
        /// <summary>Revêtement (BalulaMoto.Core.SurfaceKind).</summary>
        public int surface;
        public int rank;
        public List<Vector2> pts = new List<Vector2>();
    }
}
