using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestApp.Models
{
    public class Factory
    {
        public string Name { get; set; }
        public Product Product { get; set; }
        public double ProductionRate { get; set; }

        public Factory(string name, Product product, double productionRate)
        {
            Name = name;
            Product = product;
            ProductionRate = productionRate;
        }
    }
}
