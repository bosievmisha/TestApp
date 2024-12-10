using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestApp.Models
{
    public class Truck
    {
        public string Name { get; set; }
        public int Capacity { get; set; } // Вместимость в единицах продукции

        public Truck(string name, int capacity)
        {
            Name = name;
            Capacity = capacity;
        }
    }
}
