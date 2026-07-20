use thiserror::Error;

#[derive(Debug, Error)]
pub enum Error {
    #[error("invalid parameters: {0}")]
    Params(&'static str),
    #[error("format error: {0}")]
    Format(&'static str),
    #[error("unsupported algorithm or version")]
    Unsupported,
    #[error("random generation failed")]
    Random,
}
