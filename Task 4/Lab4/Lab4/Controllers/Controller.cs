using Contract;
using Server.Database;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;


namespace ImageServer.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ImagesController : ControllerBase
    {
        private readonly ImDB db;

        public ImagesController(ImDB db)
        {
            this.db = db;
        }

        [HttpGet]
        public async Task<List<Image>> GetImages()
        {
            var result = await db.GetImages();
            return result;
        }

        [HttpPost]
        public async Task<List<int>> AddImages([FromBody] List<Imtruc> list_images, CancellationToken token)
        {
            return await db.PostImages(list_images, token);
        }

        [HttpDelete("{id}")]
        public async Task<int> DeleteImages(int id)
        {
            var result = await db.DeleteImages(id);
            return result;
        }
    }
    [ApiController]
    [Route("api/[controller]")]
    public class CompareController : ControllerBase
    {
        private readonly ImDB db;

        public CompareController(ImDB db)
        {
            this.db = db;
        }

        [HttpPost]
        public async Task<List<float>> CompareImages(List<int> list_id, CancellationToken token)
        {
            return await db.PostCompare(list_id, token);
        }
    }
}