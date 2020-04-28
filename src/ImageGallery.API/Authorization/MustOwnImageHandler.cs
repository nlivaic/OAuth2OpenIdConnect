using System;
using System.Linq;
using System.Threading.Tasks;
using ImageGallery.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace ImageGallery.API.Authorization
{
    public class MustOwnImageHandler : AuthorizationHandler<MustOwnImageRequirement>
    {
        private readonly IHttpContextAccessor httpContextAccessor;
        private readonly IGalleryRepository galleryRepository;

        public MustOwnImageHandler(IHttpContextAccessor httpContextAccessor, IGalleryRepository galleryRepository)
        {
            this.httpContextAccessor = httpContextAccessor;
            this.galleryRepository = galleryRepository;
        }

        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, MustOwnImageRequirement requirement)
        {

            if (!Guid.TryParse(
                httpContextAccessor.HttpContext.Request.RouteValues["id"].ToString(),
                out var imageId))
            {
                context.Fail();
                return Task.CompletedTask;
            }
            var userId = httpContextAccessor.HttpContext.User.Claims.Single(c => c.Type == "sub").Value;
            if (galleryRepository.IsImageOwner(imageId, userId))
            {
                context.Fail();
                return Task.CompletedTask;
            }
            context.Succeed(requirement);
            return Task.CompletedTask;
        }
    }
}