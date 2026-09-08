namespace Portfolio.Application.Contacts;

public interface IContactService
{
    Task<ContactReceiptResponse> CreateContactAsync(
        ContactCreateRequest request,
        ContactSubmissionMetadata metadata,
        CancellationToken cancellationToken);

    Task<ContactAdminPage> GetContactsAsync(
        ContactAdminQuery query,
        CancellationToken cancellationToken);

    Task<ContactAdminResponse> GetContactAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<ContactAdminResponse> UpdateStatusAsync(
        Guid id,
        ContactStatusRequest request,
        CancellationToken cancellationToken);

    Task DeleteContactAsync(Guid id, CancellationToken cancellationToken);
}
