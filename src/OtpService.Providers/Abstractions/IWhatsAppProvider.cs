using OtpService.Core.Contracts;

namespace OtpService.Providers.Abstractions;

public interface IWhatsAppProvider
{
    Task<ProviderResult> SendAsync(SendOtpJob job, CancellationToken cancellationToken);
}