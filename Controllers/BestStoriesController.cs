using BestOfHackerNews.Dto;
using Microsoft.AspNetCore.Mvc;

namespace BestOfHackerNews.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BestStoriesController : ControllerBase
    {
        IStoryManager _storyManager;
        ILogger<BestStoriesController> _logger;

        public BestStoriesController(IStoryManager storyManager, ILogger<BestStoriesController> logger)
        {
            _logger= logger;
            _storyManager = storyManager;
        }

        [HttpGet(Name = "GetBestStories}")]
        public ActionResult<IEnumerable<StoryDto>> Get(int count)
        {
            try
            {
                _logger.LogInformation("Inside GetBestStories");
                List<StoryDto> stories=_storyManager.GetStories(count);
                return Ok(stories);
            }
            catch(Exception ex) 
            {
                _logger.LogError(ex, $"GetBestStories Failed. {ex.Message}");
                return StatusCode(500,ex.Message);
            }
        }
    }
}
