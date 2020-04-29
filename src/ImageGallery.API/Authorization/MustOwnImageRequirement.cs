using Microsoft.AspNetCore.Authorization;

namespace ImageGallery.API.Authorization
{
    /// <summary>
    /// This class is empty, but in a more complex scenario could contain data
    /// to be used by the `MustOwnImageHandler`.
    /// </summary>
    public class MustOwnImageRequirement : IAuthorizationRequirement
    {
        public MustOwnImageRequirement()
        {
        }
    }
}