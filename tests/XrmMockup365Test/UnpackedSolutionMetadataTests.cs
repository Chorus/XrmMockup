using System;
using System.Collections.Generic;
using System.IO;
using DG.Tools.XrmMockup;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Metadata;
using Xunit;
using System.Linq;

namespace XrmMockup365Test
{
    public class UnpackedSolutionMetadataTests
    {
        [Fact]
        public void CanLoadMetadataFromUnpackedSolution()
        {
            // Arrange
            var unpackedSolutionPath = @"e:\src\Autism Care\worktrees\migrate-to-xrmmockup\AutismCare.Billing\Chorus.AutismCare.Billing.Package\ChorusAutismCareBilling";
            
            if (!Directory.Exists(unpackedSolutionPath))
            {
                throw new DirectoryNotFoundException($"Test dependency not found: {unpackedSolutionPath}");
            }

            var settings = new XrmMockupSettings
            {
                MetadataDirectoryPath = unpackedSolutionPath,
                BasePluginTypes = new Type[] { }
            };

            // Act
            var instance = XrmMockup365.GetInstance(settings);
            var service = instance.GetAdminService();

            // Assert
            var entityName = "cssemr_authorization";
            var entity = new Entity(entityName);
            var id = service.Create(entity);
            
            Assert.NotEqual(Guid.Empty, id);

            var retrieved = service.Retrieve(entityName, id, new Microsoft.Xrm.Sdk.Query.ColumnSet(true));
            Assert.NotNull(retrieved);
            Assert.Contains("ownerid", retrieved.Attributes.Keys);
            Assert.Contains("createdon", retrieved.Attributes.Keys);
            Assert.Contains("statuscode", retrieved.Attributes.Keys);
        }
    }
}
