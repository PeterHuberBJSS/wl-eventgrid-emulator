using Azure;
using Azure.Messaging;
using Azure.Messaging.EventGrid.Namespaces;

var namespaceEndpoint = "http://localhost:6500/"; // "rpas-private-dev-uks-evgn.uksouth-1.eventgrid.azure.net";

// Name of the topic in the namespace
var topicName = "create-application"; //"application-updates-rpas-dev-uks-evgt";

// Access key for the topic
var topicKey = "TheLocal+DevelopmentKey="; //"15C5cMpUez8jBQ54K7y3OhEF8D9cUdNwhnr761zwDhLeumVPXmTeJQQJ99BAACmepeSXJ3w3AAABAZEGM2X6";

var subscriptionName = "dynamics-subscription"; // "application-updates-rpas-dev-uks-evgs";

var maxEventCount = 100;
var source = "dynamics-source";
var eventType = "dynamics-type";

var i = 0;

Console.WriteLine("help:");
Console.WriteLine("type q, quit or exit to close app");
Console.WriteLine("type 'push' to push an event");
Console.WriteLine("type 'pull' to pull an event");
do
{
    i++;
    var read = Console.ReadLine();

    switch (read)
    {
        case "push":
            // Construct the client using an Endpoint for a namespace as well as the access key
            var clientPush = new EventGridSenderClient(new Uri(namespaceEndpoint), topicName, new AzureKeyCredential(topicKey));

            var data3 = new Dictionary<string, object>
            {
                ["name"] = "Test 123" + i,
                ["operatorId"] = "GBP-OP-123ABC456DEF",
                ["applicationId"] = "98361a1a-d2c2-4c08-819a-33dad8b1d20" + i,
                ["status"] = 1
            };

            var cloudEvent = new CloudEvent(source, eventType, data3);
            await clientPush.SendAsync(cloudEvent);

            Console.WriteLine("Pushed index {0}", i);
            break;
        case "pull":

            await PullData(namespaceEndpoint, topicName, topicKey, subscriptionName, maxEventCount, eventType);
            break;

        case "q":
        case "quit":
        case "exit":
            return;
        default:
            break;
    }
}
while (true);

static async Task PullData(string namespaceEndpoint, string topicName, string topicKey, string subscriptionName, int maxEventCount, string eventType)
{
    var clientPull = new EventGridReceiverClient(new Uri(namespaceEndpoint), topicName, subscriptionName, new AzureKeyCredential(topicKey));
    ReceiveResult result = await clientPull.ReceiveAsync(maxEventCount);
    //Retry failed after 4 tries. Retry settings can be adjusted in ClientOptions.Retry or by configuring a custom retry policy in ClientOptions.RetryPolicy. (The operation was cancelled because it exceeded the configured timeout of 0:01:40. Network timeout can be adjusted in ClientOptions.Retry.NetworkTimeout.) (The operation was cancelled because it exceeded the configured timeout of 0:01:40. Network timeout can be adjusted in ClientOptions.Retry.NetworkTimeout.
    Console.WriteLine("Received Response {0}", result.Details.Count);
    var toAcknowledge = new List<string>();
    var toReject = new List<string>();
    foreach (var detail in result.Details)
    {
        var cloudEvent = detail.Event;
        var brokerProperties = detail.BrokerProperties;

        // The lock token is used to acknowledge, reject or release the event
        Console.WriteLine(brokerProperties.LockToken);
        Console.WriteLine();

        var data = cloudEvent.Data?.ToObjectFromJson<Dictionary<string, object>>();
        if (data != null)
        {
            foreach (var key in data.Keys)
            {
                Console.WriteLine($"Key: {key}, Value: '{data[key]}'");
                Console.WriteLine();
            }
        }
        else
        {
            Console.WriteLine("Data is null");
        }

        if (cloudEvent.Type == eventType)
        {
            toAcknowledge.Add(brokerProperties.LockToken);
        }

        // reject all other events
        else
        {
            toReject.Add(brokerProperties.LockToken);
        }
    }

    if (toAcknowledge.Count > 0)
    {
        await Acknowledge(clientPull, toAcknowledge);
    }

    if (toReject.Count > 0)
    {
        await Reject(clientPull, toReject);
    }
}

static async Task Reject(EventGridReceiverClient clientPull, List<string> toReject)
{
    RejectResult rejectResult = await clientPull.RejectAsync(toReject);

    // Inspect the Reject result
    Console.WriteLine($"Failed count for Reject: {rejectResult.FailedLockTokens.Count}");
    foreach (var failedLockToken in rejectResult.FailedLockTokens)
    {
        Console.WriteLine($"Lock Token: {failedLockToken.LockToken}");
        Console.WriteLine($"Error Code: {failedLockToken.Error}");
        Console.WriteLine($"Error Description: {failedLockToken.ToString}");
    }

    Console.WriteLine($"Success count for Reject: {rejectResult.SucceededLockTokens.Count}");
    foreach (var lockToken in rejectResult.SucceededLockTokens)
    {
        Console.WriteLine($"Lock Token: {lockToken}");
    }

    Console.WriteLine();
}

static async Task Acknowledge(EventGridReceiverClient clientPull, List<string> toAcknowledge)
{
    AcknowledgeResult acknowledgeResult = await clientPull.AcknowledgeAsync(toAcknowledge);

    // Inspect the Acknowledge result
    Console.WriteLine($"Failed count for Acknowledge: {acknowledgeResult.FailedLockTokens.Count}");
    foreach (var failedLockToken in acknowledgeResult.FailedLockTokens)
    {
        Console.WriteLine($"Lock Token: {failedLockToken.LockToken}");
        Console.WriteLine($"Error Code: {failedLockToken.Error}");
        Console.WriteLine($"Error Description: {failedLockToken.ToString}");
    }

    Console.WriteLine($"Success count for Acknowledge: {acknowledgeResult.SucceededLockTokens.Count}");
    foreach (var lockToken in acknowledgeResult.SucceededLockTokens)
    {
        Console.WriteLine($"Lock Token: {lockToken}");
    }

    Console.WriteLine();
}