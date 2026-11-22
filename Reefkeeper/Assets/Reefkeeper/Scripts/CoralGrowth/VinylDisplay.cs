using UnityEngine;

namespace Reefkeeper.Corals
{
    // Keep the scene transforms editable while sharing meshes and materials with the imported model.
    [ExecuteAlways]
    public sealed class VinylDisplay : MonoBehaviour
    {
        public string variant;
        public MeshFilter sleeve;
        public MeshFilter record;

        void OnEnable()
        {
            var model = Resources.Load<GameObject>("VinylShop/" + variant);
            if (!model) return;
            Bind(sleeve, model.transform.Find("Sleeve"));
            Bind(record, model.transform.Find("Record"));
        }

        static void Bind(MeshFilter target, Transform source)
        {
            if (!target || !source) return;
            target.sharedMesh = source.GetComponent<MeshFilter>().sharedMesh;
            target.GetComponent<MeshRenderer>().sharedMaterials = source.GetComponent<MeshRenderer>().sharedMaterials;
        }
    }
}
