using System;
using System.Collections.Generic;
using Yunus.Game.Core;
namespace Yunus.Game
{
    /// <summary>
    /// Unity composition için servis bulucu (service locator) deseni.
    /// </summary>
    public static class ServiceLocator
    {
        private static readonly Dictionary<Type, object> services = new Dictionary<Type, object>();

        /// <summary>
        /// Register  Service
        /// </summary>
        public static void Register<T>(T service) where T : class
        {
            if (service == null)
                throw new ArgumentNullException(nameof(service), "Service cannot be null.");

            var type = typeof(T);

            if (services.ContainsKey(type))
                throw new InvalidOperationException($"Service of type {type} is already registered.");

            services.Add(type, service);
        }

        public static T Resolve<T>() where T : class 
        {
            var type = typeof(T);
            
            if (!services.TryGetValue(type, out var service))
                throw new InvalidOperationException($"Service of type {type.Name} is not registered");

            return service as T;
        }

        public static void UnRegister<T>(bool cleanup = true) where T : class 
        {
            var type = typeof(T);

            if (services.TryGetValue(type, out var service))
            {
                if (cleanup && service is IService serviceInstance)
                {
                    serviceInstance.Clean();
                }
                services.Remove(type);
            }
        }
        public static void ClearAll(bool cleanup = true)
        {
            if (cleanup)
            {
                foreach (var service in services.Values)
                {
                    if (service is IService serviceInstance)
                    {
                        serviceInstance.Clean();
                    }
                }
            }
            services.Clear();
        }
        public static void TickAll()
        {
            foreach (var s in services.Values)
                if (s is ITickable t) t.Tick();
        }
        public static bool TryGet<T>(out T service) where T : class
        {
            var type = typeof(T);
            if (services.TryGetValue(type, out var obj) && obj is T ok)
            {
                service = ok;
                return true;
            }

            service = null;
            return false;
        }

        public static void InitializeAll()
        {
            foreach (var service in services.Values)
            {
                if (service is IService serviceInstance)
                {
                    serviceInstance.Initialize();
                }
            }
        }
    }
}