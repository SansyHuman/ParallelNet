using ParallelNet.Common;
using ParallelNet.Lock;

using System.Diagnostics;

ParallelNet.Collection.Queue<int> stack = new ParallelNet.Collection.Queue<int>();

stack.Enqueue(1);
stack.Enqueue(2);
stack.Enqueue(3);
stack.Enqueue(4);

foreach (int i in stack)
    Console.WriteLine(i);

int cnt = stack.Count;

for (int i = 0; i < cnt; i++)
{
    Console.WriteLine(stack.Dequeue().ResultValue);
}

List<Thread> threads = new List<Thread>();
int workPerThread = 100000;
for (int i = 0; i < Environment.ProcessorCount; i++)
{
    threads.Add(new Thread(() =>
    {
        for (int j = 0; j < workPerThread * 2; j++)
        {
            stack.Enqueue(1);
        }
    }));

    threads.Add(new Thread(() =>
    {
        for (int j = 0; j < workPerThread; j++)
        {
            if (stack.Dequeue().ResultType == Result<int, None>.Type.Failure)
            {
                j--;
                continue;
            }
        }
    }));
}

threads.ForEach(t => t.Start());
threads.ForEach(t => t.Join());

int expected = workPerThread * Environment.ProcessorCount;
int count = stack.Count;
int real = 0;

while (stack.Dequeue().ResultType == Result<int, None>.Type.Success)
    real++;

Console.WriteLine(expected == real && expected == count);

Console.WriteLine("Hello world!");