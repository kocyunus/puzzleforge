using UnityEngine;
using Yunus.Game.Core;
using Yunus.Game.Data;
using Yunus.Game.Domain.Ports;
using Yunus.Game.Domain.Services;
using Yunus.Game.Services;



namespace Yunus.Game
{
    [DefaultExecutionOrder(-1000)]
    public class GameBootstrap : MonoBehaviour
    {
        [Header("Options")]
        [SerializeField] bool initializeOnAwake = true;
        [SerializeField] bool tickServices = true;

        [SerializeField] private ColorPaletteSO colorPalette;

        private void Awake()
        {
            DontDestroyOnLoad(this.gameObject);
            RegisterServices();
            if (initializeOnAwake)
                ServiceLocator.InitializeAll();
        }

        private void Update()
        {
            if (tickServices)
                ServiceLocator.TickAll();
        }

        private void OnApplicationQuit()
        {
            ServiceLocator.ClearAll();
        }

        private void OnDestroy()
        {
            ServiceLocator.ClearAll();
        }

        private void RegisterServices()
        {
            // Color Palette
            var rgba = (colorPalette != null) ? colorPalette.ToRgba() : null;
            ServiceLocator.Register<IColorPalette>(new DistinctColorPalette(rgba));

            ServiceLocator.Register<IPrefabPooler>(new PrefabPoolerService());

            ServiceLocator.Register<IShapeScatter>(new ShapeScatterService());
            Debug.Log("[GameBootstrap] ✅ Services registered");
        }
    }
}