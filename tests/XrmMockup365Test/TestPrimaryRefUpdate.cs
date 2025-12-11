using System;
using System.IO;
using DG.Tools.XrmMockup;
using DG.XrmFramework.BusinessDomain.ServiceContext;
using Xunit;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using DG.XrmMockupTest;

namespace XrmMockup365Test
{
    public class TestPrimaryRefUpdate : UnitTestBase
    {
        public TestPrimaryRefUpdate(XrmMockupFixture fixture) : base(fixture) { }

        [Fact]
        public void TestCreateUpdatesPrimaryRefId_WorkflowExecution()
        {
            // This test verifies that when a Create request is executed, the primaryRef (Target)
            // is updated with the new ID before PostOperation workflows are executed.
            // If the ID is missing, the workflow engine will fail to retrieve the Post Image
            // and crash (ArgumentNullException in WorkflowTree.Execute).

            using (var context = new Xrm(orgAdminUIService))
            {
                // Add a workflow that triggers on Create of Account
                // This workflow (AddStringWorkflow) tries to read the account name, which requires retrieving the entity.
                // Note: This workflow has a condition that the name must be "WapAdd"
                crm.AddWorkflow(Path.Combine("../../..", "Metadata", "Workflows", "AddStringWorkflow.xml"));
                
                var acc = new Account()
                {
                    Name = "WapAdd"
                };

                // This Create call triggers the workflow.
                // If the fix is working, this should succeed.
                // If the fix is reverted, this should throw ArgumentNullException (or similar crash).
                acc.Id = orgAdminUIService.Create(acc);

                var retrieved = orgAdminUIService.Retrieve(Account.EntityLogicalName, acc.Id, new ColumnSet(true)) as Account;
                
                // Verify the workflow actually ran
                Assert.Equal("WapAdd - WapAdd ", retrieved.Name);
            }
        }
    }
}
