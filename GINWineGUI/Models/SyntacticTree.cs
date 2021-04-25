using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GrammarLearnerGUI.Models
{
   public class SyntacticTree
    {
        public string Name { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<SyntacticTree> Children  { get; set; }
    }
}
