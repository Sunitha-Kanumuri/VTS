using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SenecaGlobal.VTS.Library
{
    public sealed class MongoInstance
    {
        private static IConfiguration _conf;
        private static IMongoDatabase database;
        private static readonly MongoInstance instance;
        static MongoInstance()
        {
        }
        private MongoInstance()
        {
            MongoClient client = new MongoClient(_conf["ConnectionStrings:DefaultConnection"]);
            database = client.GetDatabase(_conf["Settings:Database"]);
        }

        public static IMongoDatabase Instance
        {
            get
            {
                if (instance == null) new MongoInstance();
                return database;
            }
        }

        public static IConfiguration Configuration
        {
            set
            {
                _conf = value;
            }
        }
    }
}
