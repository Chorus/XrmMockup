using Microsoft.Xrm.Sdk;

namespace XrmMockupShared
{
    public class MockupNotificationService : IServiceEndpointNotificationService
    {
        public string Execute(EntityReference serviceEndpoint, IExecutionContext context)
        {
            return null;
        }
    }
}
