using flowgear.Sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace flowgear.Nodes.Evision
{
    public partial class Evision : INode
    {
        public enum Actions
        {
            List,   //GET
            Create, //POST 
            Get,    //GET with key
            Update, //PUT
            Delete  //DELETE
        }
    }
}