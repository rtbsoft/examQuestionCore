using System.Linq;
using System.Threading.Tasks;
using ExamQuestion.Models;
using Microsoft.AspNetCore.SignalR;

namespace ExamQuestion.Hubs
{
    public class AllocatedMessage
    {
        public string CourseName { get; set; }
        public string ExamName { get; set; }
        public string StudentName { get; set; }
        public string DocumentNames { get; set; }
        public int ExamId { get; set; }
        public int NumDownloads{ get; set; }
    }
    public interface IAllocationClient
    {
        Task NotifyPresence(int userId);
        Task DocumentsAllocated(AllocatedMessage msg);
    }

    public class AllocationHub: Hub<IAllocationClient>
    {
        //allow the user to tell us what their connection ID is
        public async Task NotifyPresence(int userId) => await Groups.AddToGroupAsync(Context.ConnectionId, userId.ToString());
    }
}
