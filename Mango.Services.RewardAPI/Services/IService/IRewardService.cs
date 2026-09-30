using Mango.Services.RewardAPI.Message;

namespace Mango.Services.RewardAPI.Services.IService
{
    public interface IRewardService
    {
        Task UpdateRewards(RewardsMessage rewardsMessage);
    }
}
