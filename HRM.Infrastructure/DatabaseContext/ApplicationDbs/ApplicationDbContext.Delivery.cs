using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.DeliverySchema;

namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs
{
    public partial class ApplicationDbContext
    {
        public virtual DbSet<DeliveryOrder> DeliveryOrders { get; set; }
        public virtual DbSet<DeliveryOrderPO> DeliveryOrderPOs { get; set; }
        public virtual DbSet<DeliveryOrderDetail> DeliveryOrderDetails { get; set; }
        public virtual DbSet<DeliveryOrderDetailLotConsumption> DeliveryOrderDetailLotConsumptions { get; set; }
        public virtual DbSet<DelivererInfor> DelivererInfors { get; set; }
        public virtual DbSet<Deliverer> Deliverers { get; set; }
        public virtual DbSet<DeliveryVehicle> DeliveryVehicles { get; set; }
        public virtual DbSet<DeliveryTrip> DeliveryTrips { get; set; }
        public virtual DbSet<DeliveryTripOrder> DeliveryTripOrders { get; set; }
        public virtual DbSet<DeliveryStop> DeliveryStops { get; set; }
        public virtual DbSet<DeliveryExpense> DeliveryExpenses { get; set; }
        public virtual DbSet<DeliveryProofOfDelivery> DeliveryProofsOfDelivery { get; set; }
    }
}
