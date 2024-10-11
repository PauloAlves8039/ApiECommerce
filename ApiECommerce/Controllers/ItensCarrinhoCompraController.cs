using ApiECommerce.Context;
using ApiECommerce.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ApiECommerce.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ItensCarrinhoCompraController : ControllerBase
{
    private readonly AppDbContext dbContext;

    public ItensCarrinhoCompraController(AppDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    [HttpGet("{usuarioId}")]
    public async Task<IActionResult> Get(int usuarioId)
    {
        var user = await dbContext.Usuarios.FindAsync(usuarioId);

        if (user is null)
        {
            return NotFound($"Usuário com o id = {usuarioId} não encontrado");
        }

        var itensCarrinho = await (from s in dbContext.ItensCarrinhoCompra.Where(s => s.ClienteId == usuarioId)
                                   join p in dbContext.Produtos on s.ProdutoId equals p.Id
                                   select new
                                   {
                                       Id = s.Id,
                                       Preco = s.PrecoUnitario,
                                       ValorTotal = s.ValorTotal,
                                       Quantidade = s.Quantidade,
                                       ProdutoId = p.Id,
                                       ProdutoNome = p.Nome,
                                       UrlImagem = p.UrlImagem
                                   }).ToListAsync();

        return Ok(itensCarrinho);
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] ItemCarrinhoCompra itemCarrinhoCompra)
    {
        try
        {
            var carrinhoCompra = await dbContext.ItensCarrinhoCompra.FirstOrDefaultAsync(s =>
                                    s.ProdutoId == itemCarrinhoCompra.ProdutoId &&
                                    s.ClienteId == itemCarrinhoCompra.ClienteId);

            if (carrinhoCompra is not null)
            {
                carrinhoCompra.Quantidade += itemCarrinhoCompra.Quantidade;
                carrinhoCompra.ValorTotal = carrinhoCompra.PrecoUnitario * carrinhoCompra.Quantidade;
            }
            else
            {
                var produto = await dbContext.Produtos.FindAsync(itemCarrinhoCompra.ProdutoId);

                var carrinho = new ItemCarrinhoCompra()
                {
                    ClienteId = itemCarrinhoCompra.ClienteId,
                    ProdutoId = itemCarrinhoCompra.ProdutoId,
                    PrecoUnitario = itemCarrinhoCompra.PrecoUnitario,
                    Quantidade = itemCarrinhoCompra.Quantidade,
                    ValorTotal = (produto!.Preco) * (itemCarrinhoCompra.Quantidade)
                };
                dbContext.ItensCarrinhoCompra.Add(carrinho);
            }
            await dbContext.SaveChangesAsync();
            return StatusCode(StatusCodes.Status201Created);
        }
        catch (Exception)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, "Ocorreu um erro ao processar a solicitação.");
        }
    }

    [HttpPut]
    [ProducesResponseType(StatusCodes.Status200OK)] 
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)] 
    public async Task<IActionResult> Put(int produtoId, string acao)
    {
        var usuarioEmail = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;

        var usuario = await dbContext.Usuarios.FirstOrDefaultAsync(u => u.Email == usuarioEmail);

        if (usuario is null)
        {
            return NotFound("Usuário não encontrado."); 
        }

        var itemCarrinhoCompra = await dbContext.ItensCarrinhoCompra.FirstOrDefaultAsync(s =>
                                               s.ClienteId == usuario!.Id && s.ProdutoId == produtoId);

        if (itemCarrinhoCompra != null)
        {
            if (acao.ToLower() == "aumentar")
            {
                itemCarrinhoCompra.Quantidade += 1;
            }
            else if (acao.ToLower() == "diminuir")
            {
                if (itemCarrinhoCompra.Quantidade > 1)
                {
                    itemCarrinhoCompra.Quantidade -= 1;
                }
                else
                {
                    dbContext.ItensCarrinhoCompra.Remove(itemCarrinhoCompra);
                    await dbContext.SaveChangesAsync();
                    return Ok();
                }
            }
            else if (acao.ToLower() == "deletar")
            {
                dbContext.ItensCarrinhoCompra.Remove(itemCarrinhoCompra);
                await dbContext.SaveChangesAsync();
                return Ok();
            }
            else
            {
                return BadRequest("Ação Inválida. Use : 'aumentar', 'diminuir', ou 'deletar' para realizar uma ação");
            }

            itemCarrinhoCompra.ValorTotal = itemCarrinhoCompra.PrecoUnitario * itemCarrinhoCompra.Quantidade;
            await dbContext.SaveChangesAsync();
            return Ok($"Operacao : {acao} realizada com sucesso");
        }
        else
        {
            return NotFound("Nenhum item encontrado no carrinho");
        }
    }
}