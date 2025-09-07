using AutoMapper;
using BudgetTracker.Common.Models;
using BudgetTracker.Common.DTOs;

namespace BudgetTracker.API.Mappings;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<User, UserDto>()
            .ForMember(dest => dest.SubscriptionTier, opt => opt.MapFrom(src => src.SubscriptionTier.ToString()));

        CreateMap<Transaction, TransactionDto>()
            .ForMember(dest => dest.AccountName, opt => opt.MapFrom(src => src.Account.Name))
            .ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src => src.Category != null ? src.Category.Name : null))
            .ForMember(dest => dest.Type, opt => opt.MapFrom(src => src.Type.ToString()))
            .ForMember(dest => dest.Merchant, opt => opt.MapFrom(src => src.NormalizedMerchant ?? src.OriginalMerchant));

        CreateMap<CreateTransactionDto, Transaction>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.UserId, opt => opt.Ignore())
            .ForMember(dest => dest.Type, opt => opt.MapFrom(src => src.Amount < 0 ? TransactionType.Debit : TransactionType.Credit))
            .ForMember(dest => dest.OriginalMerchant, opt => opt.MapFrom(src => src.Merchant))
            .ForMember(dest => dest.PostedDate, opt => opt.MapFrom(src => src.TransactionDate))
            .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(_ => DateTime.UtcNow))
            .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(_ => DateTime.UtcNow));
    }
}