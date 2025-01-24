namespace Duccsoft;

public interface IDataSize
{
	long DataSize { get; }
	public string HumanizeDataSize() => DataSize.FormatBytes();
}
