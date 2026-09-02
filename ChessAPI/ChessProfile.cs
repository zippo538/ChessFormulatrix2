using AutoMapper;
using ChessConsole.Enums;
using ChessConsole.Model;
using ChessWeb.DTO;

namespace ChessAPI.Profiles;

public class ChessProfile : Profile
{
    public ChessProfile()
    {
        CreateMap<Postition, PosititionDTO>()
            .ForMember(dest => dest.Row, opt => opt.MapFrom(src => src.Row))
            .ForMember(dest => dest.Column, opt => opt.MapFrom(src => src.Column));

        CreateMap<Pieces, PieceDto>()
            .ForMember(dest => dest.PieceColor, opt => opt.MapFrom(src => src.PieceColor.ToString()))
            .ForMember(dest => dest.Type, opt => opt.MapFrom(src => src.Piece.ToString()))
            .ForMember(dest => dest.HasMoved, opt => opt.MapFrom(src => src.HasMoved));

        CreateMap<Tile, TileDto>();

        CreateMap<Board, BoardDTO>();

        CreateMap<Move, MoveDTO>()
            .ForMember(dest => dest.PromotionType, opt => opt.MapFrom(src => 
                src.PromotionType == PieceType.None ? null : src.PromotionType.ToString()))
            .ForMember(dest => dest.Type, opt => opt.MapFrom(src => src.Type.ToString()));

        CreateMap<MoveHistory, MoveHistoryDTO>()
            .ForMember(dest => dest.PieceType, opt => opt.MapFrom(src => src.PieceType.ToString()))
            .ForMember(dest => dest.PieceColor, opt => opt.MapFrom(src => src.PieceColor.ToString()))
            .ForMember(dest => dest.CapturedPieceType, opt => opt.MapFrom(src => 
                src.CapturedPieceType.HasValue ? src.CapturedPieceType.Value.ToString() : null))
            .AfterMap((src, dest) =>
            {
                bool isRealPromotion = src.PieceType == PieceType.Pawn &&
                                       (src.Move.To.Row == 0 || src.Move.To.Row == 7);
                if (!isRealPromotion && dest.Move != null)
                {
                    dest.Move.PromotionType = null;
                }
            });

        CreateMap<Player, PlayerDTO>()
            .ForMember(dest => dest.PlayerColor, opt => opt.MapFrom(src => src.PlayerColor.ToString()));
    }
}