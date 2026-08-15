using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

/// <summary>동시 상호작용 요청에 대한 대기형 잠금 Brick의 동작을 검증합니다.</summary>
public sealed class InteractionExecutionLockTests
{
    /// <summary>수량 2인 아이템을 동시에 요청해도 한 플레이어만 전부 획득하는지 검증합니다.</summary>
    [Test]
    public void Execute_TwoSimultaneousRequests_OnlyOnePlayerReceivesBothItems()
    {
        InteractionExecutionLock executionLock = new();
        using CountdownEvent ready = new(2);
        using ManualResetEventSlim start = new(false);

        int remainingQuantity = 2;
        int[] awardedQuantities = new int[2];

        Task firstRequest = CreateRequest(0);
        Task secondRequest = CreateRequest(1);

        Assert.That(ready.Wait(2000), Is.True,
            "두 요청이 시작 지점에 도착하지 못했습니다.");

        start.Set();

        Assert.That(
            Task.WaitAll(new[] { firstRequest, secondRequest }, 2000),
            Is.True,
            "두 요청이 제한 시간 안에 끝나지 않았습니다.");

        Assert.That(awardedQuantities.Sum(), Is.EqualTo(2));
        Assert.That(awardedQuantities.Count(quantity => quantity == 2), Is.EqualTo(1));
        Assert.That(awardedQuantities.Count(quantity => quantity == 0), Is.EqualTo(1));
        Assert.That(remainingQuantity, Is.EqualTo(0));

        Task CreateRequest(int playerIndex)
        {
            return Task.Run(() =>
            {
                ready.Signal();
                start.Wait();

                executionLock.Execute(() =>
                {
                    if (remainingQuantity <= 0)
                    {
                        return false;
                    }

                    awardedQuantities[playerIndex] = remainingQuantity;
                    remainingQuantity = 0;
                    return true;
                });
            });
        }
    }
}
