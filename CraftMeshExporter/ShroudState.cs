using UnityEngine;

namespace CraftMeshExporter
{
    internal struct ShroudState
    {
        public ModuleJettison Module;
        public bool ModuleEnabled;

        public GameObject GameObject;
        public bool GameObjectActive;

        public Renderer Renderer;
        public bool RendererEnabled;

        public ShroudState(ModuleJettison module, bool moduleEnabled)
        {
            Module = module;
            ModuleEnabled = moduleEnabled;
            GameObject = null;
            GameObjectActive = false;
            Renderer = null;
            RendererEnabled = false;
        }

        public ShroudState(GameObject gameObject, bool gameObjectActive)
        {
            Module = null;
            ModuleEnabled = false;
            GameObject = gameObject;
            GameObjectActive = gameObjectActive;
            Renderer = null;
            RendererEnabled = false;
        }

        public ShroudState(Renderer renderer, bool rendererEnabled)
        {
            Module = null;
            ModuleEnabled = false;
            GameObject = null;
            GameObjectActive = false;
            Renderer = renderer;
            RendererEnabled = rendererEnabled;
        }
    }
}
