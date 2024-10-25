using ApiECommerce.Context;
using ApiECommerce.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ApiECommerce.Controllers;

[Route("api/[controller]")]
[ApiController]
public class PedidosController : ControllerBase
{
    private readonly AppDbContext dbContext;

    public PedidosController(AppDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    [HttpGet("[action]/{pedidoId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DetalhesPedido(int pedidoId)
    {
        var pedidoDetalhes = await dbContext.DetalhesPedido.AsNoTracking()
                                   .Where(d => d.PedidoId == pedidoId)
                                   .Select(detalhePedido => new
                                   {
                                       Id = detalhePedido.Id,
                                       Quantidade = detalhePedido.Quantidade,
                                       SubTotal = detalhePedido.ValorTotal,
                                       ProdutoNome = detalhePedido.Produto!.Nome,
                                       ProdutoImagem = detalhePedido.Produto.UrlImagem,
                                       ProdutoPreco = detalhePedido.Produto.Preco
                                   })
                                   .ToListAsync();

        if (!pedidoDetalhes.Any())
        {
            return NotFound("Detalhes do pedido não encontrados.");
        }
        return Ok(pedidoDetalhes);
    }

    [HttpGet("[action]/{usuarioId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PedidosPorUsuario(int usuarioId)
    {
        var pedidos = await dbContext.Pedidos
                               .AsNoTracking()
                               .Where(pedido => pedido.UsuarioId == usuarioId)
                               .OrderByDescending(pedido => pedido.DataPedido)
                               .Select(pedido => new
                               {
                                   Id = pedido.Id,
                                   PedidoTotal = pedido.ValorTotal,
                                   DataPedido = pedido.DataPedido
                               })
                               .ToListAsync();


        if (pedidos is null || pedidos.Count == 0)
        {
            return NotFound("Não foram encontrados pedidos para o usuário especificado.");
        }
        return Ok(pedidos);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Post([FromBody] Pedido pedido)
    {
        pedido.DataPedido = DateTime.Now;

        var itensCarrinho = await dbContext.ItensCarrinhoCompra
            .Where(carrinho => carrinho.ClienteId == pedido.UsuarioId)
            .ToListAsync();

        if (itensCarrinho.Count == 0)
        {
            return NotFound("Não há itens no carrinho para criar o pedido.");
        }

        using (var transaction = await dbContext.Database.BeginTransactionAsync())
        {
            try
            {
                dbContext.Pedidos.Add(pedido);
                await dbContext.SaveChangesAsync();

                foreach (var item in itensCarrinho)
                {
                    var detalhePedido = new DetalhePedido()
                    {
                        Preco = item.PrecoUnitario,
                        ValorTotal = item.ValorTotal,
                        Quantidade = item.Quantidade,
                        ProdutoId = item.ProdutoId,
                        PedidoId = pedido.Id,
                    };
                    dbContext.DetalhesPedido.Add(detalhePedido);
                }

                await dbContext.SaveChangesAsync();
                dbContext.ItensCarrinhoCompra.RemoveRange(itensCarrinho);
                await dbContext.SaveChangesAsync();

                await transaction.CommitAsync();

                return Ok(new { OrderId = pedido.Id });
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                return BadRequest("Ocorreu um erro ao processar o pedido.");
            }
        }
    }
}
