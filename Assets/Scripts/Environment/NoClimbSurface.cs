using UnityEngine;

namespace DesertGecko.Environment
{
    /// <summary>
    /// Bu collider'ın yanlarına Gila yapışıp tırmanamaz; üstünde yürünür, yanına çarpılır.
    /// Platform/asansör geçişlerinin yanındaki kayalarda kullanılır: kaçırılan platformdan sonra
    /// duvara tutunup yolu kestirmek olmasın.
    /// </summary>
    [DisallowMultipleComponent]
    public class NoClimbSurface : MonoBehaviour
    {
    }
}
