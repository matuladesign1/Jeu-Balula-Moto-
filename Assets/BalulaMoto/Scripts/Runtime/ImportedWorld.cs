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
        [TextArea(3, 12)] public string report;
    }

    /// <summary>Axe d'une rue importée (portion dans la zone de la carte).</summary>
    [System.Serializable]
    public class ImportedRoad
    {
        public string name;
        public float width;
        public int rank;
        public List<Vector2> pts = new List<Vector2>();
    }
}
