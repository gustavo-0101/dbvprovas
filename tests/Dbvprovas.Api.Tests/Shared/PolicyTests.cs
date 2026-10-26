using System.Security.Claims;
using Dbvprovas.Api.Modules.Identity;
using Dbvprovas.Api.Modules.Tenancy;
using Microsoft.AspNetCore.Authorization;

namespace Dbvprovas.Api.Tests.Shared;

public sealed class PolicyTests
{
    // PC-09, RN-AUD-005: as políticas leem o contexto desta requisição, não a sessão.
    [Fact]
    public async Task Policies_read_the_request_context()
    {
        var context = new ClubContext { PersonId = Guid.NewGuid() };
        var handler = new PersonContextAuthorizationHandler(context);
        IAuthorizationRequirement[] requirements = [new PersonRequirement(), new PlatformAdminRequirement()];

        var first = new AuthorizationHandlerContext(requirements, new ClaimsPrincipal(), null);
        await handler.HandleAsync(first);
        context.IsPlatformAdmin = true;
        var second = new AuthorizationHandlerContext(requirements, new ClaimsPrincipal(), null);
        await handler.HandleAsync(second);
        context.PersonId = null;
        var third = new AuthorizationHandlerContext(requirements, new ClaimsPrincipal(), null);
        await handler.HandleAsync(third);

        Assert.IsType<PlatformAdminRequirement>(Assert.Single(first.PendingRequirements));
        Assert.Empty(second.PendingRequirements);
        Assert.Equal(2, third.PendingRequirements.Count());
    }
}
