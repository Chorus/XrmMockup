using System;
using System.Collections.Generic;
using System.IO;
using DG.Tools.XrmMockup;
using Microsoft.Xrm.Sdk;
using Xunit;

namespace XrmMockup365Test
{
    public class MetadataMergeTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly string _dir1;
        private readonly string _dir2;

        public MetadataMergeTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            _dir1 = Path.Combine(_tempDir, "Dir1");
            _dir2 = Path.Combine(_tempDir, "Dir2");

            Directory.CreateDirectory(_dir1);
            Directory.CreateDirectory(_dir2);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }

        [Fact]
        public void Merge_Should_Combine_Attributes_From_Multiple_Sources()
        {
            // Arrange
            CreateEntityXml(_dir1, "test_entity", isShell: true);
            CreateEntityXml(_dir2, "test_entity", isShell: false);

            var settings = new XrmMockupSettings
            {
                MetadataDirectoryPaths = new List<string> { _dir1, _dir2 },
                BasePluginTypes = new Type[] { }
            };

            // Act
            var instance = XrmMockup365.GetInstance(settings);
            var service = instance.GetAdminService();

            // Assert
            // If merge works, we should be able to create the entity and set an attribute that only exists in the full definition
            var entity = new Entity("test_entity");
            entity["test_attribute"] = "value";
            
            // If metadata is missing the attribute, Create might fail or strip it depending on settings?
            // XrmMockup usually throws if attribute is invalid for the entity.
            
            var id = service.Create(entity);
            
            var retrieved = service.Retrieve("test_entity", id, new Microsoft.Xrm.Sdk.Query.ColumnSet("test_attribute"));
            Assert.True(retrieved.Contains("test_attribute"), "Attribute from second source was not merged.");
        }

        private void CreateEntityXml(string rootDir, string entityName, bool isShell)
        {
            var entityDir = Path.Combine(rootDir, "Entities", entityName);
            Directory.CreateDirectory(entityDir);

            string content;
            if (isShell)
            {
                content = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<Entity xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
  <Name LocalizedName=""{entityName}"" OriginalName=""{entityName}"">{entityName}</Name>
  <EntityInfo>
    <entity Name=""{entityName}"">
      <EntitySetName>{entityName}s</EntitySetName>
      <OwnershipTypeMask>UserOwned</OwnershipTypeMask>
      <attributes>
        <attribute PhysicalName=""{entityName}id"">
          <Type>primarykey</Type>
          <Name>{entityName}id</Name>
          <LogicalName>{entityName}id</LogicalName>
        </attribute>
      </attributes>
    </entity>
  </EntityInfo>
  <FormXml />
  <RibbonDiffXml />
</Entity>";
            }
            else
            {
                content = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<Entity xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
  <Name LocalizedName=""{entityName}"" OriginalName=""{entityName}"">{entityName}</Name>
  <EntityInfo>
    <entity Name=""{entityName}"">
      <EntitySetName>{entityName}s</EntitySetName>
      <OwnershipTypeMask>UserOwned</OwnershipTypeMask>
      <attributes>
        <attribute PhysicalName=""test_attribute"">
          <Type>nvarchar</Type>
          <Name>test_attribute</Name>
          <LogicalName>test_attribute</LogicalName>
          <RequiredLevel>none</RequiredLevel>
          <Format>text</Format>
          <MaxLength>100</MaxLength>
          <ValidForCreateApi>1</ValidForCreateApi>
          <ValidForReadApi>1</ValidForReadApi>
          <ValidForUpdateApi>1</ValidForUpdateApi>
        </attribute>
        <attribute PhysicalName=""{entityName}id"">
          <Type>primarykey</Type>
          <Name>{entityName}id</Name>
          <LogicalName>{entityName}id</LogicalName>
        </attribute>
      </attributes>
    </entity>
  </EntityInfo>
  <FormXml />
  <RibbonDiffXml />
</Entity>";
            }

            File.WriteAllText(Path.Combine(entityDir, "Entity.xml"), content);
        }
    }
}
