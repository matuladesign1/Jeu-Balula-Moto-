// BALULA MOTO — type de chaussée d'un collisionneur (lu par les roues). Composant enregistré dans la scène
// (carte importée) : il doit être dans un fichier portant le même nom que la classe.
using BalulaMoto.Core;
using UnityEngine;

namespace BalulaMoto.Runtime
{
    /// <summary>Type de chaussée d'un collisionneur (lu par les roues).</summary>
    public class SurfaceZone : MonoBehaviour
    {
        public SurfaceKind Kind = SurfaceKind.Goudron;
    }
}
