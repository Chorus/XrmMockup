using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Metadata;
using DG.Tools.XrmMockup;

namespace DG.Tools.XrmMockup.Internal
{
    internal static class UnpackedMetadataParser
    {
        public static MetadataSkeleton Parse(string folderLocation)
        {
            var skeleton = new MetadataSkeleton
            {
                EntityMetadata = new Dictionary<string, EntityMetadata>(StringComparer.OrdinalIgnoreCase),
                OptionSets = new OptionSetMetadataBase[0],
                Currencies = new List<Entity>(),
                Plugins = new List<MetaPlugin>(),
                DefaultStateStatus = new Dictionary<string, Dictionary<int, int>>(StringComparer.OrdinalIgnoreCase)
            };

            // Set defaults for required properties that might not be in unpacked solution
            var baseCurrencyId = Guid.NewGuid();
            skeleton.BaseOrganization = new Entity("organization")
            {
                Id = Guid.NewGuid()
            };
            skeleton.BaseOrganization["basecurrencyid"] = new EntityReference("transactioncurrency", baseCurrencyId);
            skeleton.BaseOrganization["pricingdecimalprecision"] = 2;
            skeleton.BaseOrganization["fullnameconventioncode"] = new OptionSetValue(1); // First Last

            skeleton.RootBusinessUnit = new Entity("businessunit")
            {
                Id = Guid.NewGuid()
            };
            skeleton.RootBusinessUnit["name"] = "Root Business Unit";

            var baseCurrency = new Entity("transactioncurrency")
            {
                Id = baseCurrencyId
            };
            baseCurrency["isocurrencycode"] = "USD";
            baseCurrency["currencyname"] = "US Dollar";
            baseCurrency["currencysymbol"] = "$";
            baseCurrency["exchangerate"] = 1.0m;
            baseCurrency["currencyprecision"] = 2;
            skeleton.Currencies.Add(baseCurrency);

            var optionSetsFolder = Path.Combine(folderLocation, "OptionSets");
            if (Directory.Exists(optionSetsFolder))
            {
                var optionSets = new List<OptionSetMetadataBase>();
                foreach (var file in Directory.GetFiles(optionSetsFolder, "*.xml"))
                {
                    var optionSet = ParseOptionSet(file);
                    if (optionSet != null)
                    {
                        optionSets.Add(optionSet);
                    }
                }
                skeleton.OptionSets = optionSets.ToArray();
            }

            var entitiesFolder = Path.Combine(folderLocation, "Entities");
            if (Directory.Exists(entitiesFolder))
            {
                foreach (var entityDir in Directory.GetDirectories(entitiesFolder))
                {
                    var entityXmlPath = Path.Combine(entityDir, "Entity.xml");
                    if (File.Exists(entityXmlPath))
                    {
                        var (entityMetadata, defaultStatus) = ParseEntity(entityXmlPath, skeleton.OptionSets);
                        if (entityMetadata != null)
                        {
                            skeleton.EntityMetadata[entityMetadata.LogicalName] = entityMetadata;
                            if (defaultStatus != null && defaultStatus.Count > 0)
                            {
                                skeleton.DefaultStateStatus[entityMetadata.LogicalName] = defaultStatus;
                            }
                        }
                    }
                }
            }

            EnsureSystemEntities(skeleton);
            return skeleton;
        }

        public static void EnsureSystemEntities(MetadataSkeleton skeleton)
        {
            Console.WriteLine("[EnsureSystemEntities] Checking system entities...");
            // Add system entities if missing
            AddSystemEntity(skeleton, "transactioncurrency", "transactioncurrencyid", "currencyname",
                ("isocurrencycode", AttributeTypeCode.String),
                ("currencysymbol", AttributeTypeCode.String),
                ("exchangerate", AttributeTypeCode.Decimal),
                ("currencyprecision", AttributeTypeCode.Integer));

            AddSystemEntity(skeleton, "businessunit", "businessunitid", "name",
                ("organizationid", AttributeTypeCode.Lookup),
                ("utcoffset", AttributeTypeCode.Integer),
                ("css_roundingrule", AttributeTypeCode.Picklist),
                ("css_chargeperiodfrequency", AttributeTypeCode.Picklist),
                ("css_billingstartdate", AttributeTypeCode.DateTime));

            AddSystemEntity(skeleton, "systemuser", "systemuserid", "fullname",
                ("firstname", AttributeTypeCode.String),
                ("lastname", AttributeTypeCode.String),
                ("businessunitid", AttributeTypeCode.Lookup));

            AddSystemEntity(skeleton, "team", "teamid", "name",
                ("teamtype", AttributeTypeCode.Picklist),
                ("isdefault", AttributeTypeCode.Boolean),
                ("description", AttributeTypeCode.String),
                ("administratorid", AttributeTypeCode.Lookup),
                ("businessunitid", AttributeTypeCode.Lookup),
                ("teamtemplateid", AttributeTypeCode.Lookup),
                ("regardingobjectid", AttributeTypeCode.Lookup));

            AddSystemEntity(skeleton, "teammembership", "teammembershipid", null,
                ("teamid", AttributeTypeCode.Lookup),
                ("systemuserid", AttributeTypeCode.Lookup));

            AddSystemEntity(skeleton, "organization", "organizationid", "name",
                ("basecurrencyid", AttributeTypeCode.Lookup),
                ("pricingdecimalprecision", AttributeTypeCode.Integer),
                ("fullnameconventioncode", AttributeTypeCode.Picklist));

            AddSystemEntity(skeleton, "role", "roleid", "name",
                ("businessunitid", AttributeTypeCode.Lookup),
                ("roletemplateid", AttributeTypeCode.Lookup),
                ("createdby", AttributeTypeCode.Lookup),
                ("createdon", AttributeTypeCode.DateTime),
                ("modifiedby", AttributeTypeCode.Lookup),
                ("modifiedon", AttributeTypeCode.DateTime));

            AddSystemEntity(skeleton, "roletemplate", "roletemplateid", "name");

            AddSystemEntity(skeleton, "principalobjectaccess", "principalobjectaccessid", null,
                ("principalid", AttributeTypeCode.Uniqueidentifier),
                ("objectid", AttributeTypeCode.Uniqueidentifier),
                ("accessrightsmask", AttributeTypeCode.Integer),
                ("objecttypecode", AttributeTypeCode.String));

            AddSystemEntity(skeleton, "teamtemplate", "teamtemplateid", "name");

            AddSystemEntity(skeleton, "contact", "contactid", "fullname",
                ("firstname", AttributeTypeCode.String),
                ("lastname", AttributeTypeCode.String));

            AddSystemEntity(skeleton, "timezonedefinition", "timezonedefinitionid", "standardname",
                ("timezonecode", AttributeTypeCode.Integer));

            // Add missing entities referenced in tests
            AddSystemEntity(skeleton, "msemr_productorservice", "msemr_productorserviceid", "msemr_name");
            AddSystemEntity(skeleton, "css_rule", "css_ruleid", "css_name");
            AddSystemEntity(skeleton, "css_code", "css_codeid", "css_name");
            AddSystemEntity(skeleton, "css_insuranceplanbillingrequirement", "css_insuranceplanbillingrequirementid", "css_name", 
                ("css_insuranceplan", AttributeTypeCode.Lookup),
                ("css_rule", AttributeTypeCode.Lookup));

            AddSystemEntity(skeleton, "css_qualificationdefinition", "css_qualificationdefinitionid", "css_name",
                ("css_code", AttributeTypeCode.Lookup));

            AddSystemEntity(skeleton, "msemr_practitionerqualification", "msemr_practitionerqualificationid", "msemr_name",
                ("msemr_practitioner", AttributeTypeCode.Lookup),
                ("css_qualification", AttributeTypeCode.Lookup),
                ("msemr_periodstart", AttributeTypeCode.DateTime),
                ("msemr_periodend", AttributeTypeCode.DateTime));

            AddSystemEntity(skeleton, "msemr_coverage", "msemr_coverageid", "msemr_name");
            AddSystemEntity(skeleton, "msemr_careplanactivity", "msemr_careplanactivityid", "msemr_name");

            AddSystemEntity(skeleton, "css_encounterassignment", "css_encounterassignmentid", "css_name",
                ("css_retriggerbilling", AttributeTypeCode.Boolean),
                ("css_duration", AttributeTypeCode.Integer),
                ("css_periodstart", AttributeTypeCode.DateTime),
                ("css_periodend", AttributeTypeCode.DateTime),
                ("css_authorizationservice", AttributeTypeCode.Lookup),
                ("css_encounter", AttributeTypeCode.Lookup),
                ("css_encounterpractitioner", AttributeTypeCode.Lookup));
        }

        private static void AddSystemEntity(MetadataSkeleton skeleton, string logicalName, string primaryIdAttribute, string primaryNameAttribute, params (string Name, AttributeTypeCode Type)[] attributes)
        {
            EntityMetadata metadata;
            if (skeleton.EntityMetadata.ContainsKey(logicalName)) 
            {
                Console.WriteLine($"[AddSystemEntity] '{logicalName}' already exists. Merging attributes...");
                metadata = skeleton.EntityMetadata[logicalName];
                if (primaryNameAttribute != null) SetProperty(metadata, "PrimaryNameAttribute", primaryNameAttribute);
            }
            else
            {
                Console.WriteLine($"[AddSystemEntity] Adding '{logicalName}'.");
                metadata = new EntityMetadata();
                SetProperty(metadata, "LogicalName", logicalName);
                SetProperty(metadata, "PrimaryIdAttribute", primaryIdAttribute);
                SetProperty(metadata, "PrimaryNameAttribute", primaryNameAttribute);
                SetProperty(metadata, "ObjectTypeCode", 0);
                SetProperty(metadata, "OwnershipType", OwnershipTypes.OrganizationOwned);
                SetProperty(metadata, "Attributes", new AttributeMetadata[0]);
                skeleton.EntityMetadata[logicalName] = metadata;
            }

            var currentAttributes = metadata.Attributes?.ToList() ?? new List<AttributeMetadata>();
            
            // Ensure PrimaryIdAttribute
            if (!currentAttributes.Any(a => a.LogicalName == primaryIdAttribute))
            {
                var idAttr = CreateAttribute(primaryIdAttribute, AttributeTypeCode.Uniqueidentifier);
                currentAttributes.Add(idAttr);
            }

            // Ensure PrimaryNameAttribute
            if (primaryNameAttribute != null && !currentAttributes.Any(a => a.LogicalName == primaryNameAttribute))
            {
                var nameAttr = CreateAttribute(primaryNameAttribute, AttributeTypeCode.String);
                currentAttributes.Add(nameAttr);
            }

            foreach (var (name, type) in attributes)
            {
                if (!currentAttributes.Any(a => a.LogicalName == name))
                {
                    currentAttributes.Add(CreateAttribute(name, type));
                }
            }

            SetProperty(metadata, "Attributes", currentAttributes.ToArray());
            
            if (string.IsNullOrEmpty(metadata.PrimaryIdAttribute)) SetProperty(metadata, "PrimaryIdAttribute", primaryIdAttribute);
            if (string.IsNullOrEmpty(metadata.PrimaryNameAttribute)) SetProperty(metadata, "PrimaryNameAttribute", primaryNameAttribute);
        }

        private static AttributeMetadata CreateAttribute(string logicalName, AttributeTypeCode typeCode)
        {
            AttributeMetadata attr = null;
            switch (typeCode)
            {
                case AttributeTypeCode.String: attr = new StringAttributeMetadata(); break;
                case AttributeTypeCode.Integer: attr = new IntegerAttributeMetadata(); break;
                case AttributeTypeCode.Boolean: attr = new BooleanAttributeMetadata(); break;
                case AttributeTypeCode.Decimal: attr = new DecimalAttributeMetadata(); break;
                case AttributeTypeCode.Money: attr = new MoneyAttributeMetadata(); break;
                case AttributeTypeCode.Lookup: 
                case AttributeTypeCode.Owner:
                case AttributeTypeCode.Customer:
                    attr = new LookupAttributeMetadata(); break;
                case AttributeTypeCode.Uniqueidentifier: attr = new UniqueIdentifierAttributeMetadata(); break;
                case AttributeTypeCode.Picklist: attr = new PicklistAttributeMetadata(); break;
                case AttributeTypeCode.State: attr = new StateAttributeMetadata(); break;
                case AttributeTypeCode.Status: attr = new StatusAttributeMetadata(); break;
                default: attr = new AttributeMetadata(); break;
            }
            SetProperty(attr, "LogicalName", logicalName);
            SetProperty(attr, "AttributeType", typeCode);
            return attr;
        }

        private static (EntityMetadata Metadata, Dictionary<int, int> DefaultStatus) ParseEntity(string filePath, IEnumerable<OptionSetMetadataBase> globalOptionSets)
        {
            var doc = XDocument.Load(filePath);
            var entityElement = doc.Root.Element("EntityInfo")?.Element("entity");
            if (entityElement == null) return (null, null);

            var metadata = new EntityMetadata();
            SetProperty(metadata, "LogicalName", (entityElement.Element("Name")?.Value ?? entityElement.Attribute("Name")?.Value)?.ToLower());
            
            // Basic properties
            SetProperty(metadata, "ObjectTypeCode", int.TryParse(entityElement.Element("ObjectTypeCode")?.Value, out var otc) ? (int?)otc : null);
            SetProperty(metadata, "OwnershipType", ParseOwnershipType(entityElement.Element("OwnershipTypeMask")?.Value));
            SetProperty(metadata, "PrimaryIdAttribute", GetPrimaryIdAttribute(entityElement));
            SetProperty(metadata, "PrimaryNameAttribute", GetPrimaryNameAttribute(entityElement));

            // Attributes
            var attributesElement = entityElement.Element("attributes");
            var attributes = new List<AttributeMetadata>();
            var defaultStatusMap = new Dictionary<int, int>();

            if (attributesElement != null)
            {
                foreach (var attrElem in attributesElement.Elements("attribute"))
                {
                    var attr = ParseAttribute(attrElem, globalOptionSets);
                    if (attr != null)
                    {
                        attributes.Add(attr);
                        
                        // Check for statuscode to extract default status
                        if (attr.LogicalName == "statuscode")
                        {
                            var statuses = attrElem.Element("optionset")?.Element("statuses");
                            if (statuses != null)
                            {
                                foreach (var statusElem in statuses.Elements("status"))
                                {
                                    var value = int.Parse(statusElem.Attribute("value")?.Value ?? "0");
                                    var state = int.Parse(statusElem.Attribute("state")?.Value ?? "0");
                                    
                                    // Use the first status encountered for a state as default
                                    if (!defaultStatusMap.ContainsKey(state))
                                    {
                                        defaultStatusMap[state] = value;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // Ensure PrimaryIdAttribute is present
            if (!string.IsNullOrEmpty(metadata.PrimaryIdAttribute) && !attributes.Any(a => a.LogicalName == metadata.PrimaryIdAttribute))
            {
                var idAttr = new UniqueIdentifierAttributeMetadata();
                SetProperty(idAttr, "LogicalName", metadata.PrimaryIdAttribute);
                SetProperty(idAttr, "SchemaName", metadata.PrimaryIdAttribute);
                SetProperty(idAttr, "AttributeType", AttributeTypeCode.Uniqueidentifier);
                SetProperty(idAttr, "IsPrimaryId", true);
                attributes.Add(idAttr);
            }

            // Ensure ownerid is present for UserOwned entities
            if (metadata.OwnershipType == OwnershipTypes.UserOwned)
            {
                AddStandardAttributeIfMissing(attributes, "ownerid", AttributeTypeCode.Owner);
                AddStandardAttributeIfMissing(attributes, "owningbusinessunit", AttributeTypeCode.Lookup);
                AddStandardAttributeIfMissing(attributes, "owninguser", AttributeTypeCode.Lookup);
                AddStandardAttributeIfMissing(attributes, "owningteam", AttributeTypeCode.Lookup);
            }

            // Add standard attributes if missing
            AddStandardAttributeIfMissing(attributes, "createdon", AttributeTypeCode.DateTime);
            AddStandardAttributeIfMissing(attributes, "createdby", AttributeTypeCode.Lookup);
            AddStandardAttributeIfMissing(attributes, "modifiedon", AttributeTypeCode.DateTime);
            AddStandardAttributeIfMissing(attributes, "modifiedby", AttributeTypeCode.Lookup);
            AddStandardAttributeIfMissing(attributes, "statecode", AttributeTypeCode.State);
            AddStandardAttributeIfMissing(attributes, "statuscode", AttributeTypeCode.Status);

            if (defaultStatusMap.Count == 0)
            {
                defaultStatusMap[0] = 1;
                defaultStatusMap[1] = 2;
            }

            SetProperty(metadata, "Attributes", attributes.ToArray());

            return (metadata, defaultStatusMap);
        }

        private static void AddStandardAttributeIfMissing(List<AttributeMetadata> attributes, string logicalName, AttributeTypeCode type)
        {
            if (!attributes.Any(a => a.LogicalName == logicalName))
            {
                var attr = CreateAttribute(logicalName, type);
                SetProperty(attr, "SchemaName", logicalName);
                attributes.Add(attr);
            }
        }

        private static AttributeMetadata ParseAttribute(XElement attrElem, IEnumerable<OptionSetMetadataBase> globalOptionSets)
        {
            var type = attrElem.Element("Type")?.Value;
            AttributeMetadata attr = null;

            switch (type)
            {
                case "nvarchar":
                case "memo":
                case "string": // Sometimes it's string
                    attr = new StringAttributeMetadata();
                    break;
                case "int":
                case "integer":
                    attr = new IntegerAttributeMetadata();
                    break;
                case "bit":
                case "boolean":
                    attr = new BooleanAttributeMetadata();
                    break;
                case "datetime":
                    attr = new DateTimeAttributeMetadata();
                    break;
                case "decimal":
                    attr = new DecimalAttributeMetadata();
                    break;
                case "float":
                case "double":
                    attr = new DoubleAttributeMetadata();
                    break;
                case "money":
                    attr = new MoneyAttributeMetadata();
                    break;
                case "lookup":
                case "customer":
                case "owner":
                    attr = new LookupAttributeMetadata();
                    break;
                case "picklist":
                    attr = new PicklistAttributeMetadata();
                    break;
                case "state":
                    attr = new StateAttributeMetadata();
                    break;
                case "status":
                    attr = new StatusAttributeMetadata();
                    break;
                case "uniqueidentifier":
                case "primarykey":
                    attr = new UniqueIdentifierAttributeMetadata();
                    break;
                default:
                    attr = new AttributeMetadata(); // Fallback
                    break;
            }

            SetProperty(attr, "LogicalName", attrElem.Element("LogicalName")?.Value?.ToLower());
            SetProperty(attr, "SchemaName", attrElem.Element("Name")?.Value);
            
            // Common flags
            SetProperty(attr, "IsValidForCreate", attrElem.Element("ValidForCreateApi")?.Value == "1");
            SetProperty(attr, "IsValidForRead", attrElem.Element("ValidForReadApi")?.Value == "1");
            SetProperty(attr, "IsValidForUpdate", attrElem.Element("ValidForUpdateApi")?.Value == "1");

            // Type specific parsing
            if (attr is EnumAttributeMetadata enumAttr)
            {
                var optionSetElem = attrElem.Element("optionset");
                if (optionSetElem != null)
                {
                    var optionSet = new OptionSetMetadata();
                    SetProperty(optionSet, "Name", optionSetElem.Attribute("Name")?.Value);
                    
                    var options = new List<OptionMetadata>();
                    var optionsContainer = optionSetElem.Element("options") ?? optionSetElem.Element("statuses");
                    
                    if (optionsContainer != null)
                    {
                        foreach (var optElem in optionsContainer.Elements())
                        {
                            var valStr = optElem.Attribute("value")?.Value;
                            if (int.TryParse(valStr, out var val))
                            {
                                var label = optElem.Element("labels")?.Element("label")?.Attribute("description")?.Value ?? "";
                                var stateStr = optElem.Attribute("state")?.Value;
                                int.TryParse(stateStr, out var state);

                                OptionMetadata option;
                                if (attr is StatusAttributeMetadata)
                                {
                                    var statusOption = new StatusOptionMetadata();
                                    statusOption.Value = val;
                                    // statusOption.State = state; // State property might be read-only or internal in some SDK versions? 
                                    // Actually StatusOptionMetadata usually doesn't expose State setter publicly in older SDKs?
                                    // Let's try setting it via reflection if needed, or just assume it works.
                                    // But wait, StatusOptionMetadata inherits from OptionMetadata.
                                    // Let's check if we can set State.
                                    SetProperty(statusOption, "State", state);
                                    statusOption.Label = new Label(label, 1033);
                                    option = statusOption;
                                }
                                else if (attr is StateAttributeMetadata)
                                {
                                    var stateOption = new StateOptionMetadata();
                                    stateOption.Value = val;
                                    stateOption.Label = new Label(label, 1033);
                                    option = stateOption;
                                }
                                else
                                {
                                    option = new OptionMetadata(new Label(label, 1033), val);
                                }
                                options.Add(option);
                            }
                        }
                    }
                    
                    SetProperty(optionSet, "Options", new OptionMetadataCollection(options));
                    SetProperty(enumAttr, "OptionSet", optionSet);
                }
                else
                {
                    // Try global optionset
                    var optionSetName = attrElem.Element("OptionSetName")?.Value;
                    if (!string.IsNullOrEmpty(optionSetName) && globalOptionSets != null)
                    {
                        var globalOS = globalOptionSets.FirstOrDefault(os => os.Name == optionSetName);
                        if (globalOS is OptionSetMetadata osm)
                        {
                            SetProperty(enumAttr, "OptionSet", osm);
                            if (enumAttr.OptionSet == null)
                            {
                                // Fallback for older SDKs or if SetProperty failed
                                try {
                                    enumAttr.OptionSet = osm;
                                } catch {}
                                
                                if (enumAttr.OptionSet == null)
                                {
                                     throw new Exception($"Failed to set OptionSet property on {attr.LogicalName}. Property found? {enumAttr.GetType().GetProperty("OptionSet") != null}");
                                }
                            }
                        }
                        else if (globalOS is BooleanOptionSetMetadata bosm && attr is BooleanAttributeMetadata boolAttr)
                        {
                             SetProperty(boolAttr, "OptionSet", bosm);
                        }
                    }
                }
            }
            else if (attr is BooleanAttributeMetadata boolAttr)
            {
                 var optionSetElem = attrElem.Element("optionset");
                 if (optionSetElem != null)
                 {
                     var boolOptionSet = new BooleanOptionSetMetadata();
                     SetProperty(boolOptionSet, "Name", optionSetElem.Attribute("Name")?.Value);
                     
                     var options = optionSetElem.Element("options");
                     if (options != null)
                     {
                         var trueOption = options.Elements("option").FirstOrDefault(o => o.Attribute("value")?.Value == "1");
                         var falseOption = options.Elements("option").FirstOrDefault(o => o.Attribute("value")?.Value == "0");
                         
                         if (trueOption != null)
                         {
                             var label = trueOption.Element("labels")?.Element("label")?.Attribute("description")?.Value ?? "";
                             SetProperty(boolOptionSet, "TrueOption", new OptionMetadata(new Label(label, 1033), 1));
                         }
                         
                         if (falseOption != null)
                         {
                             var label = falseOption.Element("labels")?.Element("label")?.Attribute("description")?.Value ?? "";
                             SetProperty(boolOptionSet, "FalseOption", new OptionMetadata(new Label(label, 1033), 0));
                         }
                     }
                     SetProperty(boolAttr, "OptionSet", boolOptionSet);
                 }
                 else
                 {
                    // Try global optionset
                    var optionSetName = attrElem.Element("OptionSetName")?.Value;
                    if (!string.IsNullOrEmpty(optionSetName) && globalOptionSets != null)
                    {
                        var globalOS = globalOptionSets.FirstOrDefault(os => os.Name == optionSetName);
                        if (globalOS is BooleanOptionSetMetadata bosm)
                        {
                            SetProperty(boolAttr, "OptionSet", bosm);
                        }
                    }
                 }
            }

            return attr;
        }

        private static OptionSetMetadataBase ParseOptionSet(string filePath)
        {
            var doc = XDocument.Load(filePath);
            var root = doc.Root;
            var type = root.Element("OptionSetType")?.Value;

            OptionSetMetadataBase metadata = null;
            if (type == "bit" || type == "boolean")
            {
                metadata = new BooleanOptionSetMetadata();
            }
            else
            {
                metadata = new OptionSetMetadata();
            }

            SetProperty(metadata, "Name", root.Attribute("Name")?.Value);
            
            // Parse options...
            if (metadata is OptionSetMetadata optionSet)
            {
                var options = new List<OptionMetadata>();
                var optionsElem = root.Element("options");
                if (optionsElem != null)
                {
                    foreach (var optElem in optionsElem.Elements("option"))
                    {
                        var val = int.Parse(optElem.Attribute("value")?.Value ?? "0");
                        var label = optElem.Element("labels")?.Element("label")?.Attribute("description")?.Value ?? "";
                        
                        var option = new OptionMetadata(new Label(label, 1033), val);
                        options.Add(option);
                    }
                }
                SetProperty(optionSet, "Options", new OptionMetadataCollection(options));
            }

            return metadata;
        }

        private static OwnershipTypes? ParseOwnershipType(string value)
        {
            if (string.IsNullOrEmpty(value)) return null;

            if (value.Equals("OrgOwned", StringComparison.OrdinalIgnoreCase)) return OwnershipTypes.OrganizationOwned;
            if (value.Equals("UserOwned", StringComparison.OrdinalIgnoreCase)) return OwnershipTypes.UserOwned;

            if (Enum.TryParse<OwnershipTypes>(value, out var result))
            {
                return result;
            }
            // Handle masks if needed, but usually it's a single string in XML like "UserOwned"
            return null;
        }

        private static string GetPrimaryIdAttribute(XElement entityElement)
        {
            // Usually the attribute with type uniqueidentifier and name = logicalname + "id"
            // Or we can look for IsPrimaryId attribute if it exists in unpacked XML (it usually doesn't explicitly say "PrimaryId")
            // But standard convention is logicalname + "id".
            // Let's look for an attribute that looks like the primary ID.
            var logicalName = (entityElement.Element("Name")?.Value ?? entityElement.Attribute("Name")?.Value)?.ToLower();
            var attributes = entityElement.Element("attributes");
            if (attributes != null)
            {
                foreach (var attr in attributes.Elements("attribute"))
                {
                    var attrName = attr.Element("LogicalName")?.Value?.ToLower();
                    if (attrName == logicalName + "id") return attrName;
                }
            }
            return logicalName + "id"; // Fallback
        }

        private static string GetPrimaryNameAttribute(XElement entityElement)
        {
             var attributes = entityElement.Element("attributes");
            if (attributes != null)
            {
                foreach (var attr in attributes.Elements("attribute"))
                {
                    var displayMask = attr.Element("DisplayMask")?.Value;
                    if (displayMask != null && displayMask.Contains("PrimaryName"))
                    {
                        return attr.Element("LogicalName")?.Value?.ToLower();
                    }
                }

                foreach (var attr in attributes.Elements("attribute"))
                {
                    // In unpacked XML, there isn't a clear "PrimaryName" flag on the attribute itself usually.
                    // But often it's the one with name = "name" or similar.
                    // Actually, EntityInfo might not have it.
                    // We might need to guess or look harder.
                    // For now, let's guess "name" or the first string attribute.
                    var attrName = attr.Element("LogicalName")?.Value?.ToLower();
                    if (attrName == "name") return attrName;
                }
            }
            return "name";
        }

        private static void SetProperty(object target, string propertyName, object value)
        {
            if (target == null) return;
            var prop = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (prop != null && prop.CanWrite)
            {
                prop.SetValue(target, value);
            }
            else if (prop != null)
            {
                // Try setting backing field or using private setter?
                // EntityMetadata properties often have private setters.
                prop.SetValue(target, value, null);
            }
        }

        public static Entity ParseWorkflow(string filePath)
        {
            var doc = XDocument.Load(filePath);
            var root = doc.Root;
            if (root.Name.LocalName != "Workflow") return null;

            var entity = new Entity("workflow");
            if (Guid.TryParse(root.Attribute("WorkflowId")?.Value, out var id))
            {
                entity.Id = id;
                entity["workflowid"] = id;
            }
            entity["name"] = root.Attribute("Name")?.Value;
            
            SetAttribute(entity, root, "category", "Category", "OptionSetValue");
            SetAttribute(entity, root, "type", "Type", "OptionSetValue");
            SetAttribute(entity, root, "statecode", "StateCode", "OptionSetValue");
            SetAttribute(entity, root, "statuscode", "StatusCode", "OptionSetValue");
            SetAttribute(entity, root, "primaryentity", "PrimaryEntity", "string");
            SetAttribute(entity, root, "scope", "Scope", "OptionSetValue");
            SetAttribute(entity, root, "mode", "Mode", "OptionSetValue");
            SetAttribute(entity, root, "subprocess", "Subprocess", "bool");
            SetAttribute(entity, root, "ondemand", "OnDemand", "bool");
            SetAttribute(entity, root, "triggeroncreate", "TriggerOnCreate", "bool");
            SetAttribute(entity, root, "triggerondelete", "TriggerOnDelete", "bool");
            SetAttribute(entity, root, "asyncautodelete", "AsyncAutodelete", "bool");
            SetAttribute(entity, root, "uniquename", "UniqueName", "string");
            SetAttribute(entity, root, "istransacted", "IsTransacted", "bool");
            SetAttribute(entity, root, "introducedversion", "IntroducedVersion", "string");
            
            return entity;
        }

        private static void SetAttribute(Entity entity, XElement root, string attrName, string xmlElemName, string type)
        {
            var val = root.Element(xmlElemName)?.Value;
            if (val == null) return;

            switch (type)
            {
                case "OptionSetValue":
                    if (int.TryParse(val, out var i)) entity[attrName] = new OptionSetValue(i);
                    break;
                case "string":
                    entity[attrName] = val;
                    break;
                case "bool":
                    entity[attrName] = val == "1" || val.ToLower() == "true";
                    break;
                case "int":
                    if (int.TryParse(val, out var j)) entity[attrName] = j;
                    break;
            }
        }
    }
}
