using Saldo.Application.Interfaces;
using Saldo.Application.UseCases;
using Saldo.Domain.Entities;

namespace Saldo.Tests.Unit.UseCases;

public sealed class CategoryColorTests
{
    [Theory]
    [InlineData(" #a1b2c3 ", "#A1B2C3")]
    [InlineData("#000000", "#000000")]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    public async Task AddAndEdit_NormalizeColorAndAllowClearing(string? input, string? expected)
    {
        var repository = new CategoryRepositoryFake();
        var added = await new AddCategory(repository).ExecuteAsync(" New category ", input);

        Assert.Equal(expected, added.ColorCode);
        Assert.Equal("New category", added.Name);

        await new EditCategory(repository).ExecuteAsync(1, " Renamed category ", input);

        Assert.Equal(expected, repository.Existing.ColorCode);
        Assert.Equal("Renamed category", repository.Existing.Name);
        Assert.Equal(1, repository.UpdateCalls);
    }

    [Theory]
    [InlineData("#123")]
    [InlineData("123456")]
    [InlineData("#12345678")]
    [InlineData("#GG0000")]
    [InlineData("red")]
    public async Task AddAndEdit_InvalidColor_DoNotWriteOrChangeExistingCategory(string input)
    {
        var repository = new CategoryRepositoryFake();

        var addError = await Assert.ThrowsAsync<ArgumentException>(
            () => new AddCategory(repository).ExecuteAsync("New category", input));
        var editError = await Assert.ThrowsAsync<ArgumentException>(
            () => new EditCategory(repository).ExecuteAsync(1, "Changed name", input));

        Assert.Equal("colorCode", addError.ParamName);
        Assert.Equal("colorCode", editError.ParamName);
        Assert.Null(repository.Added);
        Assert.Equal(0, repository.UpdateCalls);
        Assert.Equal("Existing category", repository.Existing.Name);
        Assert.Equal("#112233", repository.Existing.ColorCode);
    }

    private sealed class CategoryRepositoryFake : ICategoryRepository
    {
        public Category Existing { get; } = new() { Id = 1, Name = "Existing category", ColorCode = "#112233" };
        public Category? Added { get; private set; }
        public int UpdateCalls { get; private set; }

        public Task<Category?> GetByIdAsync(int id, CancellationToken ct = default)
            => Task.FromResult<Category?>(id == Existing.Id ? Existing : null);

        public Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Category>>([Existing]);

        public Task AddAsync(Category category, CancellationToken ct = default)
        {
            Added = category;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Category category, CancellationToken ct = default)
        {
            UpdateCalls++;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(int id, CancellationToken ct = default)
            => throw new NotSupportedException();
    }
}
