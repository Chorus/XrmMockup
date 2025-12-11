using System;
using System.Collections.Generic;
using System.IO;
using DG.Tools.XrmMockup;
using Microsoft.Xrm.Sdk;
using Xunit;

namespace XrmMockup365Test
{
    public class MetadataMultiPathTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly string _dirFull;
        private readonly string _dirEmpty;

        public MetadataMultiPathTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            _dirFull = Path.Combine(_tempDir, "Full");
            _dirEmpty = Path.Combine(_tempDir, "Empty");

            Directory.CreateDirectory(_dirFull);
            Directory.CreateDirectory(_dirEmpty);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }

        [Fact]
        public void Merge_Should_Keep_Attributes_When_Loading_Full_Then_Empty()
        {
            // Arrange
            CreateEntityXml(_dirFull, "test_entity", isShell: false);
            CreateEntityXml(_dirEmpty, "test_entity", isShell: true);

            var settings = new XrmMockupSettings
            {
                MetadataDirectoryPaths = new List<string> { _dirFull, _dirEmpty },
                BasePluginTypes = new Type[] { }
            };

            // Act
            var instance = XrmMockup365.GetInstance(settings);
            var service = instance.GetAdminService();

            // Assert
            var entity = new Entity("test_entity");
            entity["test_attribute"] = "value";
            
            var id = service.Create(entity);
            var retrieved = service.Retrieve("test_entity", id, new Microsoft.Xrm.Sdk.Query.ColumnSet("test_attribute"));
            Assert.True(retrieved.Contains("test_attribute"), "Attribute should exist when loading Full then Empty");
        }

        [Fact]
        public void Merge_Should_Keep_Attributes_When_Loading_Empty_Then_Full()
        {
            // Arrange
            CreateEntityXml(_dirFull, "test_entity", isShell: false);
            CreateEntityXml(_dirEmpty, "test_entity", isShell: true);

            var settings = new XrmMockupSettings
            {
                MetadataDirectoryPaths = new List<string> { _dirEmpty, _dirFull },
                BasePluginTypes = new Type[] { }
            };

            // Act
            var instance = XrmMockup365.GetInstance(settings);
            var service = instance.GetAdminService();

            // Assert
            var entity = new Entity("test_entity");
            entity["test_attribute"] = "value";
            
            var id = service.Create(entity);
            var retrieved = service.Retrieve("test_entity", id, new Microsoft.Xrm.Sdk.Query.ColumnSet("test_attribute"));
            Assert.True(retrieved.Contains("test_attribute"), "Attribute should exist when loading Empty then Full");
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
