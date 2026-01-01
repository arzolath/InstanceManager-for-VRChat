using System;

namespace InstanceManager.Core.Errors;

public interface IExceptionReporter
{
    void Report(Exception ex, string context);
}
