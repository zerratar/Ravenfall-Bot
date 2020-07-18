﻿using Newtonsoft.Json;

namespace RavenfallOfficial.Core
{
    public class AppSettingsProvider
    {
        public IAppSettings Get()
        {
            var text = System.IO.File.ReadAllText("settings.json");
            return JsonConvert.DeserializeObject<AppSettings>(text);
        }
    }
}