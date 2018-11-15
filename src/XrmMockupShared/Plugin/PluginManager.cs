using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System.ServiceModel;
using Microsoft.Xrm.Sdk.Metadata;
using System.Runtime.ExceptionServices;
using XrmMockupShared;
using System.Collections;
using Newtonsoft.Json;

namespace DG.Tools.XrmMockup {

    // StepSubscription           : ClassName, ExecutionStage, EventOperation, LogicalName
    // StepDeployment   : Deployment, ExecutionMode, Name, ExecutionOrder, FilteredAttributes, UserContext
    // StepImage           : Name, EntityAlias, ImageType, Attributes

    public struct StepConfig
    {
        public StepSubscription StepSubscription;
        public StepDeployment StepDeployment;
        public IEnumerable<StepImage> StepImages;

        public StepConfig(StepSubscription stepSubscription, StepDeployment stepDeployment, IEnumerable<StepImage> stepImages) :this()
        {
            StepSubscription = stepSubscription;
            StepDeployment = stepDeployment;
            StepImages = stepImages;
        }
    }

    public struct StepSubscription {
        public string ClassName;
        public int ExecutionStage;
        public string EventOperation;
        public string LogicalName;

        public StepSubscription(string className, int pluginExecutionStage, string pluginEventOperation, string logicalName) : this()
        {
            ClassName = className;
            ExecutionStage = pluginExecutionStage;
            EventOperation = pluginEventOperation;
            LogicalName = logicalName;
        }
    }
    public struct StepDeployment {
        public int Deployment;
        public int ExecutionMode;
        public string Name;
        public int ExecutionOrder;
        public string FilteredAttributes;
        public string UserContext;
        private readonly int IsolationMode;

        public StepDeployment(int pluginDeployment, int pluginExecutionMode, string name, int executionOrder, string filteredAttributes, string userContext, int isolationMode) : this()
        {
            Deployment = pluginDeployment;
            ExecutionMode = pluginExecutionMode;
            Name = name;
            ExecutionOrder = executionOrder;
            FilteredAttributes = filteredAttributes;
            UserContext = userContext;
            this.IsolationMode = isolationMode;
        }
    }
    public struct StepImage {
        public string Name;
        public string EntityAlias;
        public int ImageType;
        public string Attributes;

        public StepImage(string name, string entityAlias, int pluginImageType, string attributes) : this()
        {
            Name = name;
            EntityAlias = entityAlias;
            ImageType = pluginImageType;
            Attributes = attributes;
        }
    }

    internal class PluginManager {

        private Dictionary<EventOperation, Dictionary<ExecutionStage, List<PluginTrigger>>> registeredPlugins;
        private Dictionary<EventOperation, Dictionary<ExecutionStage, List<PluginTrigger>>> temporaryPlugins;
        private Dictionary<EventOperation, Dictionary<ExecutionStage, List<PluginTrigger>>> registeredSystemPlugins;
        private bool disableRegisteredPlugins = false;

        // List of SystemPlugins to execute
        private List<MockupPlugin> systemPlugins = new List<MockupPlugin>
        {
            //new SystemPlugins.ContactDefaultValues()
        };

        public PluginManager(IEnumerable<Type> basePluginTypes, Dictionary<string, EntityMetadata> metadata, List<MetaPlugin> plugins)
        {
            registeredPlugins = new Dictionary<EventOperation, Dictionary<ExecutionStage, List<PluginTrigger>>>();
            temporaryPlugins = new Dictionary<EventOperation, Dictionary<ExecutionStage, List<PluginTrigger>>>();
            registeredSystemPlugins = new Dictionary<EventOperation, Dictionary<ExecutionStage, List<PluginTrigger>>>();

            RegisterPlugins(basePluginTypes, metadata, plugins, registeredPlugins);
            RegisterSystemPlugins(registeredSystemPlugins);
        }

        private void RegisterPlugins(IEnumerable<Type> basePluginTypes, Dictionary<string, EntityMetadata> metadata, List<MetaPlugin> plugins, Dictionary<EventOperation, Dictionary<ExecutionStage, List<PluginTrigger>>> register)
        {
            foreach (var basePluginType in basePluginTypes)
            {
                if (basePluginType == null) continue;
                Assembly proxyTypeAssembly = basePluginType.Assembly;

                foreach (var type in proxyTypeAssembly.GetLoadableTypes())
                {
                    if (!basePluginType.IsAssignableFrom(type) || type.IsAbstract || type.GetConstructor(Type.EmptyTypes) == null) continue;
                    RegisterPlugin(type, metadata, plugins, register);
                }
            }
            SortAllLists(register);
        }

        private void RegisterPlugin(Type basePluginType, Dictionary<string, EntityMetadata> metadata, List<MetaPlugin> plugins, Dictionary<EventOperation, Dictionary<ExecutionStage, List<PluginTrigger>>> register)
        {
            var plugin = Activator.CreateInstance(basePluginType);

            Action<MockupServiceProviderAndFactory> pluginExecute = null;
            var stepConfigs = new List<StepConfig>();

            if (basePluginType.GetMethod("PluginProcessingStepConfigs") != null)
            { // Matches DAXIF plugin registration
                var configs = basePluginType
                    .GetMethod("PluginProcessingStepConfigs")
                    .Invoke(plugin, new object[] { });
                var stepConfig = JsonConvert.DeserializeObject<IEnumerable<StepConfig>>(JsonConvert.SerializeObject(configs));
                stepConfigs.AddRange(stepConfig);
                pluginExecute = (provider) => {
                    basePluginType
                    .GetMethod("Execute")
                    .Invoke(plugin, new object[] { provider });
                };
            }
            else
            { // Retrieve registration from CRM metadata
                var metaPlugin = plugins.FirstOrDefault(x => x.AssemblyName == basePluginType.FullName);
                if (metaPlugin == null)
                {
                    throw new MockupException($"Unknown plugin '{basePluginType.FullName}', please use DAXIF registration or make sure the plugin is uploaded to CRM.");
                }
                var stepConfig = new StepSubscription(metaPlugin.AssemblyName, metaPlugin.Stage, metaPlugin.MessageName, metaPlugin.PrimaryEntity);
                var extendedConfig = new StepDeployment(0, metaPlugin.Mode, metaPlugin.Name, metaPlugin.Rank, metaPlugin.FilteredAttributes, Guid.Empty.ToString(), metaPlugin.AssemblyIsolationMode);
                var imageTuple = new List<StepImage>();
                stepConfigs.Add(new StepConfig(stepConfig, extendedConfig, imageTuple));
                pluginExecute = (provider) => {
                    basePluginType
                    .GetMethod("Execute")
                    .Invoke(plugin, new object[] { provider });
                };
            }

            // Add discovered plugin triggers
            foreach (var stepConfig in stepConfigs)
            {
                var operation = (EventOperation)Enum.Parse(typeof(EventOperation), stepConfig.StepSubscription.EventOperation);
                var stage = (ExecutionStage)stepConfig.StepSubscription.ExecutionStage;

                //stepConfig.StepDeployment.Deployment

                var trigger = new PluginTrigger(operation, stage, pluginExecute, stepConfig, metadata);

                AddTrigger(operation, stage, trigger, register);
            }
        }

        public void ResetPlugins()
        {
            disableRegisteredPlugins = false;
            temporaryPlugins = new Dictionary<EventOperation, Dictionary<ExecutionStage, List<PluginTrigger>>>();
        }

        public void DisabelRegisteredPlugins(bool disable)
        {
            disableRegisteredPlugins = disable;
        }

        public void RegisterAdditionalPlugin(Type pluginType, Dictionary<string, EntityMetadata> metadata, List<MetaPlugin> plugins, PluginRegistrationScope scope)
        {
            if (pluginType.GetMethod("PluginProcessingStepConfigs") == null)
                throw new MockupException($"Unknown plugin '{pluginType.FullName}', please use the MockPlugin to register your plugin.");
            if (scope == PluginRegistrationScope.Permanent)
            {
                RegisterPlugin(pluginType, metadata, plugins, registeredPlugins);
            }
            else if (scope == PluginRegistrationScope.Temporary)
            {
                RegisterPlugin(pluginType, metadata, plugins, temporaryPlugins);
            }
        }

        private void RegisterSystemPlugins(Dictionary<EventOperation, Dictionary<ExecutionStage, List<PluginTrigger>>> register)
        {
            Action<MockupServiceProviderAndFactory> pluginExecute = null;
            var stepConfigs = new List<StepConfig>();

            foreach (var plugin in systemPlugins)
            {
                stepConfigs.AddRange(plugin.PluginProcessingStepConfigs());
                pluginExecute = (provider) => plugin.Execute(provider);

                // Add discovered plugin triggers
                foreach (var stepConfig in stepConfigs)
                {
                    var operation = (EventOperation)Enum.Parse(typeof(EventOperation), stepConfig.StepSubscription.EventOperation);
                    var stage = (ExecutionStage)stepConfig.StepSubscription.ExecutionStage;
                    var trigger = new PluginTrigger(operation, stage, pluginExecute, stepConfig, new Dictionary<string, EntityMetadata>());

                    AddTrigger(operation, stage, trigger, register);
                }
            }
            SortAllLists(register);
        }

        public void AddTrigger(EventOperation operation, ExecutionStage stage, PluginTrigger trigger, Dictionary<EventOperation, Dictionary<ExecutionStage, List<PluginTrigger>>> register) {
            if (!register.ContainsKey(operation)) {
                register.Add(operation, new Dictionary<ExecutionStage, List<PluginTrigger>>());
            }
            if (!register[operation].ContainsKey(stage)) {
                register[operation].Add(stage, new List<PluginTrigger>());
            }
            register[operation][stage].Add(trigger);
        }

        /// <summary>
        /// Sorts all the registered which shares the same entry point based on their given order
        /// </summary>
        private void SortAllLists(Dictionary<EventOperation, Dictionary<ExecutionStage, List<PluginTrigger>>> plugins)
        {
            foreach (var dictEntry in plugins)
            {
                foreach (var listEntry in dictEntry.Value)
                {
                    listEntry.Value.Sort();
                }
            }
        }

        /// <summary>
        /// Trigger all plugin steps which match the given parameters.
        /// </summary>
        /// <param name="operation"></param>
        /// <param name="stage"></param>
        /// <param name="entity"></param>
        /// <param name="preImage"></param>
        /// <param name="postImage"></param>
        /// <param name="pluginContext"></param>
        /// <param name="core"></param>
        public void Trigger(EventOperation operation, ExecutionStage stage,
                object entity, Entity preImage, Entity postImage, PluginContext pluginContext, Core core) {
            
            if (!disableRegisteredPlugins && registeredPlugins.ContainsKey(operation) && registeredPlugins[operation].ContainsKey(stage))
                registeredPlugins[operation][stage].ForEach(p => p.ExecuteIfMatch(entity, preImage, postImage, pluginContext, core));
            if(temporaryPlugins.ContainsKey(operation) && temporaryPlugins[operation].ContainsKey(stage))
                temporaryPlugins[operation][stage].ForEach(p => p.ExecuteIfMatch(entity, preImage, postImage, pluginContext, core));
        }

        public void TriggerSystem(EventOperation operation, ExecutionStage stage,
                object entity, Entity preImage, Entity postImage, PluginContext pluginContext, Core core)
        {
            if (!this.registeredSystemPlugins.ContainsKey(operation)) return;
            if (!this.registeredSystemPlugins[operation].ContainsKey(stage)) return;

            registeredSystemPlugins[operation][stage].ForEach(p => p.ExecuteIfMatch(entity, preImage, postImage, pluginContext, core));
        }


        internal class PluginTrigger : IComparable<PluginTrigger> {
            public Action<MockupServiceProviderAndFactory> pluginExecute;

            string entityName;
            EventOperation operation;
            ExecutionStage stage;
            ExecutionMode mode;
            int order = 0;
            Dictionary<string, EntityMetadata> metadata;

            HashSet<string> attributes;
            IEnumerable<StepImage> images;

            public PluginTrigger(EventOperation operation, ExecutionStage stage,
                    Action<MockupServiceProviderAndFactory> pluginExecute, StepConfig stepConfig, Dictionary<string, EntityMetadata> metadata) {
                this.pluginExecute = pluginExecute;
                this.entityName = stepConfig.StepSubscription.LogicalName;
                this.operation = operation;
                this.stage = stage;
                this.mode = (ExecutionMode)stepConfig.StepDeployment.ExecutionMode;
                this.order = stepConfig.StepDeployment.ExecutionOrder;
                this.images = stepConfig.StepImages;
                this.metadata = metadata;

                var attrs = stepConfig.StepDeployment.FilteredAttributes ?? "";
                this.attributes = String.IsNullOrWhiteSpace(attrs) ? new HashSet<string>() : new HashSet<string>(attrs.Split(','));
            }

            public void ExecuteIfMatch(object entityObject, Entity preImage, Entity postImage, PluginContext pluginContext, Core core) {
                // Check if it is supposed to execute. Returns preemptively, if it should not.
                var entity = entityObject as Entity;
                var entityRef = entityObject as EntityReference;
                var request = entityObject as OrganizationRequest;

                var guid = entity?.Id ?? entityRef?.Id ?? Guid.Empty;

                var logicalName =  entity?.LogicalName ?? entityRef?.LogicalName ?? request?.RequestName;

                if (entityName != "" && entityName != logicalName) return;

                if (entity != null && metadata.GetMetadata(logicalName)?.PrimaryIdAttribute != null) {
                    entity[metadata.GetMetadata(logicalName).PrimaryIdAttribute] = guid;
                }

                if (pluginContext.Depth > 8) {
                    throw new FaultException(
                        "This workflow job was canceled because the workflow that started it included an infinite loop." +
                        " Correct the workflow logic and try again.");
                }

                if (operation == EventOperation.Update && stage == ExecutionStage.PostOperation) {
                    var shadowAddedAttributes = postImage.Attributes.Where(a => !preImage.Attributes.ContainsKey(a.Key) && !entity.Attributes.ContainsKey(a.Key));
                    entity = entity.CloneEntity();
                    entity.Attributes.AddRange(shadowAddedAttributes);
                }

                if (operation == EventOperation.Update && attributes.Count > 0) {
                    var foundAttr = false;
                    foreach (var attr in entity.Attributes) {
                        if (attributes.Contains(attr.Key)) {
                            foundAttr = true;
                            break;
                        }
                    }
                    if (!foundAttr) return;
                }

                if (entityName != "" && (operation == EventOperation.Associate || operation == EventOperation.Disassociate)) {
                    throw new MockupException(
                        $"An {operation} plugin step was registered for a specific entity, which can only be registered on AnyEntity");
                }

                // Create the plugin context
                var thisPluginContext = pluginContext.Clone();
                thisPluginContext.Mode = (int)this.mode;
                thisPluginContext.Stage = (int)this.stage;
                if (thisPluginContext.PrimaryEntityId == Guid.Empty) { 
                    thisPluginContext.PrimaryEntityId = guid;
                }
                thisPluginContext.PrimaryEntityName = logicalName;

                foreach (var image in this.images) {
                    var type = (ImageType)image.ImageType;
                    var cols = image.Attributes != null ? new ColumnSet(image.Attributes.Split(',')) : new ColumnSet(true);
                    if (postImage != null && stage == ExecutionStage.PostOperation && (type == ImageType.PostImage || type == ImageType.Both)) {
                        thisPluginContext.PostEntityImages.Add(image.Name, postImage.CloneEntity(metadata.GetMetadata(postImage.LogicalName), cols));
                    }
                    if (preImage != null && type == ImageType.PreImage || type == ImageType.Both) {
                        thisPluginContext.PreEntityImages.Add(image.Name, preImage.CloneEntity(metadata.GetMetadata(preImage.LogicalName), cols));
                    }
                }

                // Create service provider and execute the plugin
                MockupServiceProviderAndFactory provider = new MockupServiceProviderAndFactory(core, thisPluginContext, new TracingService(), new MockupNotificationService());
                try {
                    pluginExecute(provider);
                } catch (TargetInvocationException e) {
                    ExceptionDispatchInfo.Capture(e.InnerException).Throw();
                }

                foreach (var parameter in thisPluginContext.SharedVariables)
                {
                    pluginContext.SharedVariables[parameter.Key] = parameter.Value;
                }
    }

            public int CompareTo(PluginTrigger other) {
                return this.order.CompareTo(other.order);
            }
        }
    }
}
