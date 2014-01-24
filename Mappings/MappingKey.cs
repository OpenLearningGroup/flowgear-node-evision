using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace flowgear.Nodes.Evision
{
    class MappingKey
    {
        public string Module { get; set; }
        public string Action { get; set; }

        public override string ToString()
        {
            return string.Format("{0}|{1}", Module, Action);
        }

        public MappingKey(string module, string action)
        {
            Module = module;
            Action = action;
        }

        public MappingKey(Evision.Modules module, Evision.Actions action)
        {
            Module = module.ToString();
            Action = action.ToString();
        }
    }
}
