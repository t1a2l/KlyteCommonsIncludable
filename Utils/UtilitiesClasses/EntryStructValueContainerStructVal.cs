﻿using System.Xml.Serialization;


namespace Commons.Utils.UtilitiesClasses
{
    [XmlRoot("Entry")]
    public class EntryStructValueContainerStructVal<TKey, TValue> where TKey : class where TValue : struct
    {
        [XmlAttribute("key")]
        public TKey Id { get; set; }

        [XmlAttribute("value")]
        public TValue Value { get; set; }
    }

}
