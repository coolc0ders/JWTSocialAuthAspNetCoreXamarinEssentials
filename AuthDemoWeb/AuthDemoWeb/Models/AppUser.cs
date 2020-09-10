using AspNetCore.Identity.Mongo.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AuthDemoWeb.Models
{
    public class AppUser : MongoUser
    {
        public string FirstName { get; set; }
        public string SecondName { get; set; }
        public string PictureURL { get; set; }
    }
}
