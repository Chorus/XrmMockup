using System;
using Microsoft.Xrm.Sdk;
using DG.Tools.XrmMockup;
using Xunit;
using DG.XrmMockupTest;
using XrmPluginCore;
using System.Collections.Generic;

namespace XrmMockup365Test
{
    public class TestTagPropagation : UnitTestBase
    {
        private XrmMockupFixture _fixture;

        public TestTagPropagation(XrmMockupFixture fixture) : base(fixture) 
        {
            _fixture = fixture;
        }

        [Fact]
        public void TestTagIsPropagatedToPlugin()
        {
            Console.WriteLine($"[Test] TagCheckPlugin Assembly: {typeof(TestPluginAssembly365.Plugins.TagCheckPlugin).Assembly.GetName().Name}");
            Console.WriteLine($"[Test] TagCheckPlugin FullName: {typeof(TestPluginAssembly365.Plugins.TagCheckPlugin).FullName}");

            // We need to register the plugin manually for this test since it's not in the default fixture
            var settings = new XrmMockupSettings
            {
                BasePluginTypes = new Type[] { typeof(TestPluginAssembly365.Plugins.TagCheckPlugin) },
                MetadataDirectoryPath = _fixture.Settings.MetadataDirectoryPath,
                IPluginMetadata = new MetaPlugin[]
                {
                    new MetaPlugin
                    {
                        PluginTypeAssemblyName = "TestPluginAssembly365",
                        AssemblyName = "TestPluginAssembly365.Plugins.TagCheckPlugin",
                        MessageName = "Create",
                        PrimaryEntity = "contact",
                        Stage = 20 // PreOperation
                    }
                }
            };

            var localCrm = XrmMockup365.GetInstance(settings);
            var localService = localCrm.GetAdminService();

            var contact = new Entity("contact");
            contact["lastname"] = "Test Contact";

            var request = new OrganizationRequest("Create");
            request["Target"] = contact;
            request.Parameters["tag"] = "MyTagValue";

            var response = localService.Execute(request);
            var id = (Guid)response.Results["id"];

            var retrieved = localService.Retrieve("contact", id, new Microsoft.Xrm.Sdk.Query.ColumnSet("description"));
            
            Assert.True(retrieved.Contains("description"), "Description field should be populated by the plugin");
            Assert.Equal("TagFound: MyTagValue", retrieved["description"]);
        }
    }
}
