﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace MyQbt
{
    [XmlRoot("Config")]
    public class Config
    {
        [XmlRoot("Connect")]
        public class Connect
        {
            [XmlAttribute("Url")]
            public string Url;
            [XmlAttribute("User")]
            public string User;
            [XmlAttribute("Password")]
            public string Password;
        }

        [XmlAttribute("LastUseUrl")]
        public string LastUseUrl;
        [XmlArray("ConnectList")]
        public List<Connect> ConnectList;

        [XmlAttribute("LastUseCategory")]
        public string LastUseCategory;
        [XmlArray("CategoryList")]
        public List<string> CategoryList;

        [XmlElement("DiskMapString")]
        public string DiskMapString;
    }
}