namespace Flowly.MessageInfrastructure.Registration;

internal interface ICrossProviderConflictValidator
{
    void Validate(IEnumerable<ProviderQueueManifest> manifests);
}
