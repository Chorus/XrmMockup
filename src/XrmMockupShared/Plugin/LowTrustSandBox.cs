using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Security.Permissions;
using System.Security.Policy;
using System.Text;

namespace DG.Tools.XrmMockup
{
    /// <summary>
    /// A facade for an <see cref="AppDomain"/> with partial trust privileges.
    /// </summary>
    public class LowTrustSandBox : IDisposable
    {
        private AppDomain _appDomain;

        #region Constructor(s)

        /// <summary>
        /// Creates a low trust <see cref="LowTrustSandBox"/> instance.
        /// </summary>
        /// <param name="fullTrustAssemblies">Strong named assemblies that will have full trust in the sandbox.</param>
        public LowTrustSandBox(params Assembly[] fullTrustAssemblies)
            : this(null, fullTrustAssemblies)
        { }

        /// <summary>
        /// Creates a partial trust <see cref="LowTrustSandBox"/> instance with a given set of permissions.
        /// </summary>
        /// <param name="permissions">Optional <see cref="LowTrustSandBox"/> permission set. By default a minimal trust
        /// permission set is used.</param>
        /// <param name="fullTrustAssemblies">Strong named assemblies that will have full trust in the sandbox.</param>
        public LowTrustSandBox(PermissionSet permissions, params Assembly[] fullTrustAssemblies)
        {
            var setup = new AppDomainSetup { ApplicationBase = AppDomain.CurrentDomain.BaseDirectory };

            var strongNames = new HashSet<StrongName>();

            //// Grant full trust to XrmMockup assembly to enable use of NUnit assertions in sandboxed test code.
            //strongNames.Add(GetStrongName(typeof(XrmMockupBase).Assembly));
            if (fullTrustAssemblies != null)
            {
                foreach (var assembly in fullTrustAssemblies)
                {
                    strongNames.Add(GetStrongName(assembly));
                }
            }

            _appDomain = AppDomain.CreateDomain(
                "TestSandBox" + DateTime.Now.Ticks, null, setup,
                permissions ?? GetLowTrustPermissionSet(),
                strongNames.ToArray());
        }

        #endregion

        #region Finalizer and Dispose methods

        /// <summary>
        /// The <see cref="LowTrustSandBox"/> finalizer.
        /// </summary>
        ~LowTrustSandBox()
        {
            Dispose(false);
        }

        /// <summary>
        /// Unloads the <see cref="AppDomain"/>.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        /// Unloads the <see cref="AppDomain"/>.
        /// </summary>
        /// <param name="disposing">Indicates whether this method is called from <see cref="Dispose()"/>.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_appDomain != null)
            {
                AppDomain.Unload(_appDomain);
                _appDomain = null;
            }
        }

        #endregion

        #region PermissionSet factory methods
        public static PermissionSet GetLowTrustPermissionSet()
        {
            var permissions = new PermissionSet(PermissionState.Unrestricted);
            permissions.AddPermission(new SecurityPermission(
                SecurityPermissionFlag.Execution |                  // Required to execute test code
                SecurityPermissionFlag.SerializationFormatter));    // Required to support cross-appdomain test result formatting by NUnit TestContext
            permissions.AddPermission(new ReflectionPermission(
                ReflectionPermissionFlag.MemberAccess| ReflectionPermissionFlag.AllFlags));            // Required to instantiate classes that contain test code and to get cross-appdomain communication to work.
            return permissions;
        }

        #endregion

        #region Run methods

        public T Run<T>(Func<T> func)
        {
            return (T)Run(func.Method);
        }

        public void Run(Action action)
        {
            Run(action.Method);
        }
        public object Run(MethodInfo method, params object[] parameters)
        {
            if (method == null) throw new ArgumentNullException(nameof(method));
            if (_appDomain == null) throw new ObjectDisposedException(null);

            var methodRunnerType = typeof(MethodRunner);
            var methodRunnerProxy = (MethodRunner)_appDomain.CreateInstanceAndUnwrap(
                methodRunnerType.Assembly.FullName, methodRunnerType.FullName);

            try
            {
                return methodRunnerProxy.Run(method, parameters);
            }
            catch (Exception e)
            {
                throw e is TargetInvocationException
                    ? e.InnerException
                    : e;
            }
        }

        #endregion

        #region Private methods

        private static StrongName GetStrongName(Assembly assembly)
        {
            AssemblyName assemblyName = assembly.GetName();

            byte[] publicKey = assembly.GetName().GetPublicKey();
            if (publicKey == null || publicKey.Length == 0)
            {
                throw new InvalidOperationException("Assembly is not strongly named");
            }

            return new StrongName(new StrongNamePublicKeyBlob(publicKey), assemblyName.Name, assemblyName.Version);
        }

        #endregion

        #region Inner classes

        [Serializable]
        internal class MethodRunner : MarshalByRefObject
        {
            public object Run(MethodInfo method, params object[] parameters)
            {
                var instance = method.IsStatic
                    ? null
                    : Activator.CreateInstance(method.ReflectedType);
                try
                {
                    return method.Invoke(instance, parameters);
                }
                catch (TargetInvocationException e)
                {
                    if (e.InnerException == null) throw;
                    throw e.InnerException;
                }
            }
        }

        #endregion
    }
}
