using POS.Application.Common;

namespace POS.Application.Abstractions.Persistence;

public interface IDatabaseFailureClassifier
{
    DatabaseFailureKind? Classify(Exception exception);
}
